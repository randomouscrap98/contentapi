using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using AutoMapper;
using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;

namespace contentapi.Search;

/// <summary>
/// Configuration for the generic searcher
/// </summary>
/// <remarks>
/// Note to self: start making this stupid config classes have reasonable defaults,
/// I don't feel like always configuring everything in configs, and I set up a system
/// to easily load from config if I need it.
/// </remarks>
public class GenericSearcherConfig
{
    public int MaxIndividualResultSet {get;set;} = 1000;
}

//This should probably move out at some point
public class SearchRequestPlus : SearchRequest
{
    public RequestType requestType {get;set;}
    public TypeInfo typeInfo {get;set;} = new TypeInfo();
}

public class GenericSearcher : IGenericSearch
{
    protected ILogger logger;
    protected IDbConnection dbcon;
    protected ITypeInfoService typeService;
    protected GenericSearcherConfig config;
    protected ISearchQueryParser parser;
    protected IMapper mapper;

    public const string MainAlias = "main";
    public const string DescendingAppend = "_desc";
    public const string SystemPrepend = "_sys";
    
    //Should this be configurable? I don't care for now
    protected readonly Dictionary<RequestType, Type> StandardSelect = new Dictionary<RequestType, Type> {
        { RequestType.user, typeof(UserView) },
        { RequestType.comment, typeof(CommentView) },
        { RequestType.content, typeof(ContentView) },
        { RequestType.page, typeof(PageView) },
        { RequestType.module, typeof(ModuleView) },
        { RequestType.file, typeof(FileView) }
    };

    protected readonly List<RequestType> ContentTypes = new List<RequestType>()
    {
        RequestType.content, RequestType.file, RequestType.page, RequestType.module
    };

    public const string LastPostDateField = nameof(ContentView.lastPostDate);
    public const string QuantizationField = nameof(FileView.quantization);
    public const string DescriptionField = nameof(ModuleView.description);
    public const string InternalTypeStringField = nameof(ContentView.internalTypeString);

    //These fields are too difficult to modify with the attributes, so we do it in code here
    protected readonly Dictionary<(RequestType, string),string> ModifiedFields = new Dictionary<(RequestType, string), string> {
        { (RequestType.content, LastPostDateField), $"(select createDate from comments where {MainAlias}.id = contentId order by id desc limit 1) as {LastPostDateField}" },
        { (RequestType.file, QuantizationField), $"(select value from content_values where {MainAlias}.id = contentId and key='{QuantizationField}' limit 1) as {QuantizationField}" },
        { (RequestType.module, DescriptionField), $"(select value from content_values where {MainAlias}.id = contentId and key='{DescriptionField}' limit 1) as {DescriptionField}" },
        { (RequestType.user, "registered"), $"(registrationKey IS NULL) as registered" }
    };

    //Some searches can easily be modified afterwards with constants, put that here.
    protected readonly Dictionary<RequestType, string> GeneralSearchModifiers = new Dictionary<RequestType, string>()
    {
       { RequestType.file, $"internalType = {(int)InternalContentType.file}" } ,
       { RequestType.module, $"internalType = {(int)InternalContentType.module}" },
       { RequestType.page, $"internalType = {(int)InternalContentType.page}" } ,
       { RequestType.comment, $"module IS NULL" } 
    };

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, GenericSearcherConfig config, IMapper mapper,
        ISearchQueryParser parser)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
        this.config = config;
        this.mapper = mapper;
        this.parser = parser;

        //Duplicate some fields
        var lpdSelect = ModifiedFields[(RequestType.content, LastPostDateField)];
        ModifiedFields.Add((RequestType.file, LastPostDateField), lpdSelect);
        ModifiedFields.Add((RequestType.page, LastPostDateField), lpdSelect);
        ModifiedFields.Add((RequestType.module, LastPostDateField), lpdSelect);

        foreach(var type in ContentTypes)
        {
            ModifiedFields.Add((type, InternalTypeStringField), EnumToCase<InternalContentType>(nameof(Db.Content.internalType), InternalTypeStringField));
        }
    }

    public string EnumToCase<T>(string fieldName, string outputName) where T : struct, System.Enum 
    {
        var values = Enum.GetValues<T>();
        var whens = values.Select(x => $"WHEN {Unsafe.As<T, int>(ref x)} THEN '{x.ToString()}'");
        return $"CASE {fieldName} {string.Join(" ", whens)} ELSE 'unknown' END as {outputName}";
    }

    public string SystemKey(SearchRequest request, string field)
    {
        return $"{SystemPrepend}_{request.name}_{field}";
    }

    /// <summary>
    /// Throw exceptions on any funky business we can quickly report
    /// </summary>
    /// <param name="requests"></param>
    public void RequestPrecheck(SearchRequests requests)
    {
        var acceptedTypes = Enum.GetNames<RequestType>();

        foreach(var request in requests.requests)
        {
            //Oops, unknown type
            if(!acceptedTypes.Contains(request.type))
                throw new ArgumentException($"Unknown request type: {request.type} in request {request.name}");
            
            //Users HAVE to name their stuff! Otherwise the output of the data
            //dictionary won't make any sense! Note that the method for this check 
            //isn't EXACTLY right but... it should be fine, fields are all
            //just regular identifiers, like in a programming language.
            if(!parser.IsFieldNameValid(request.name))
                throw new ArgumentException($"Malformed name '{request.name}'");
        }
    }

    /// <summary>
    /// Return the field selector for the given field. For instance, it might be a 
    /// simple "username", or it might be "(registered IS NULL) AS registered" etc.
    /// </summary>
    /// <remarks> MOST fields should work with this function, either it's the same name,
    /// or it's a simple remap from an attribute, or it's slightly complex but stored
    /// in our dictionary of remaps </remarks>
    /// <param name="fieldName"></param>
    /// <param name="r"></param>
    /// <returns></returns>
    public string StandardFieldRemap(string fieldName, SearchRequestPlus r)
    {
        //Our personal field modifiers always override all
        if (ModifiedFields.ContainsKey((r.requestType, fieldName)))
            return ModifiedFields[(r.requestType, fieldName)];
        //Return whatever's in the fieldmap, EVEN if it's empty (if it's empty, we remove it from the selector)
        else if (r.typeInfo.fieldRemap.ContainsKey(fieldName))
            return r.typeInfo.fieldRemap[fieldName];
        //Just a basic fieldname replacement
        else
            return fieldName;
    }

    /// <summary>
    /// This method adds a 'standard' select for regular searches against simple
    /// single table queries. For instance, it might be "SELECT id,username FROM users "
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    public void AddStandardSelect(StringBuilder queryStr, SearchRequestPlus r)
    {
        var fields = r.fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); 

        //Redo fieldlist if they asked for special formats
        if (r.fields == "*")
            fields = new List<string>(r.typeInfo.queryableFields);
        
        //Check for bad fields
        foreach (var field in fields)
            if (!r.typeInfo.queryableFields.Contains(field))
                throw new ArgumentException($"Unknown field {field} in request {r.name}");

        var fieldSelect = fields.Select(x => StandardFieldRemap(x, r)).Where(x => !string.IsNullOrEmpty(x)).ToList();

        queryStr.Append("SELECT ");
        queryStr.Append(string.Join(",", fieldSelect));
        queryStr.Append(" FROM ");
        queryStr.Append(r.typeInfo.database ?? throw new InvalidOperationException($"Standard select {r.type} doesn't map to database table in request {r.name}!"));
        queryStr.Append($" AS {MainAlias} ");
    }

    /// <summary>
    /// This method performs a "standard" user-query parse and adds the generated sql to the 
    /// given queryStr, along with any missing parameters that were able to be computed.
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="request"></param>
    /// <param name="parameters"></param>
    public string CreateStandardQuery(StringBuilder queryStr, SearchRequestPlus request, Dictionary<string, object> parameters)
    {
        //Not sure if the "query" is generic enough to be placed outside of standard... mmm maybe
        try
        {
            var parseResult = parser.ParseQuery(request.query, f =>
            {
                if (request.typeInfo.searchableFields.Contains(f))
                    return f;
                else
                    throw new ArgumentException($"Field {f} not searchable yet in type {request.type}({request.name})!");
            }, v =>
            {
                var realValName = v.TrimStart('@');
                if (!parameters.ContainsKey(realValName))
                {
                        //There are two options: one is that it's just deeper in, the other
                        //is that it just doesn't exist. Keep going down and down through dot
                        //operators until we reach the actual value, and if it exists, add
                        //it with the full path (dots converted to underscores) to the values list
                        throw new ArgumentException($"Unknown value {v} in {request.name}");
                }
                return v;
            });

            return parseResult;

        }
        catch (Exception ex)
        {
            //Convert to argument exception so the user knows what's up
            logger.LogWarning($"Exception during query parse: {ex}");
            throw new ArgumentException(ex.Message);
        }
    }

    public string AddQueryClause(string baseQuery, string clause)
    {
        if(string.IsNullOrWhiteSpace(baseQuery))
            return clause;
        else if (!string.IsNullOrWhiteSpace(clause))
            return $"({baseQuery}) AND ({clause})";
        return baseQuery; 
    }

    /// <summary>
    /// Assuming a query that can be limited simply, this adds the necessary LIMIT, OFFSET,
    /// and ORDER BY clauses. Most queries can use this function.
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    /// <param name="parameters"></param>
    public void BasicFinalLimit(StringBuilder queryStr, SearchRequestPlus r, Dictionary<string, object> parameters)
    {
        if(!string.IsNullOrEmpty(r.order))
        {
            var order = r.order;
            var descending = false;

            if(r.order.EndsWith(DescendingAppend))
            {
                descending = true;
                order = r.order.Substring(0, r.order.Length - DescendingAppend.Length);
            }

            if(!r.typeInfo.queryableFields.Contains(order))
                throw new ArgumentException($"Unknown order field {order} for request {r.name}");

            //Can't parameterize the column... inserting directly; scary
            queryStr.Append($"ORDER BY {order} ");

            if(descending)
                queryStr.Append("DESC ");
        }

        //ALWAYS need limit if you're doing offset, just easier to include it. -1 is magic
        var limit = Math.Min(r.limit > 0 ? r.limit : int.MaxValue, config.MaxIndividualResultSet);
        var limitKey = SystemKey(r, "limit");
        queryStr.Append($"LIMIT @{limitKey} ");
        parameters.Add(limitKey, limit);

        if (r.skip > 0)
        {
            var skipKey = SystemKey(r, "skip");
            queryStr.Append($"OFFSET @{skipKey} ");
            parameters.Add(skipKey, limit);
        }
    }

    //All searches are reads, don't need to open the connection OR set up transactions, wow.
    public async Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> Search(SearchRequests requests)
    {
        var result = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();
        var modifiedValues = new Dictionary<string, object>(requests.values);
        var queryStr = new StringBuilder();

        //Before wasting the user's time on useless junk, check some simple stuff
        //Might look silly to do this loop twice but whatever, be nice to the users
        RequestPrecheck(requests);

        foreach(var request in requests.requests.Select(x => mapper.Map<SearchRequestPlus>(x)))
        {
            request.requestType = Enum.Parse<RequestType>(request.type); //I know this will succeed
            request.typeInfo = typeService.GetTypeInfo(StandardSelect[request.requestType]);
            queryStr.Clear();

            if(StandardSelect.ContainsKey(request.requestType))
            {
                //Generate "select from"
                AddStandardSelect(queryStr, request);                   

                //Generate "where (x)"
                var query = CreateStandardQuery(queryStr, request, modifiedValues);    
                query = AddQueryClause(query, GeneralSearchModifiers.GetValueOrDefault(request.requestType, ""));
                if (!string.IsNullOrWhiteSpace(query))
                    queryStr.Append($"WHERE {query} ");

                //Generate "order by limit offset"
                BasicFinalLimit(queryStr, request, modifiedValues);     
            }
            else
            {
                throw new NotImplementedException($"Sorry, {request.type} isn't ready yet!");
            }

            var sql = queryStr.ToString();
            logger.LogDebug($"Running SQL for {request.type}({request.name}): {sql}");

            var dp = new DynamicParameters(modifiedValues);
            var qresult = (await dbcon.QueryAsync(sql, dp)).Cast<IDictionary<string, object>>();

            //Add the results to the USER results, AND add it to our list of values so it can
            //be used in chaining. It's just a reference, don't worry about duplication or whatever.
            result.Add(request.name, qresult);
            modifiedValues.Add(request.name, qresult);
        }

        return result;
    }
}