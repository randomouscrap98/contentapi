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
public class GenericSearcherConfig
{
    public int MaxIndividualResultSet {get;set;} = 1000;
}

//This should probably move out at some point
public class SearchRequestPlus : SearchRequest
{
    public RequestType requestType {get;set;}
    public TypeInfo typeInfo {get;set;} = new TypeInfo();
    public List<string> requestFields {get;set;} = new List<string>();
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
    protected readonly Dictionary<RequestType, Type> StandardViewRequests = new Dictionary<RequestType, Type> {
        { RequestType.user, typeof(UserView) },
        { RequestType.comment, typeof(CommentView) },
        { RequestType.content, typeof(ContentView) },
        { RequestType.page, typeof(PageView) },
        { RequestType.module, typeof(ModuleView) },
        { RequestType.file, typeof(FileView) }
    };

    protected readonly List<RequestType> ContentRequestTypes = new List<RequestType>()
    {
        RequestType.content, RequestType.file, RequestType.page, RequestType.module
    };

    public const string LastPostDateField = nameof(ContentView.lastPostDate);
    public const string LastPostIdField = nameof(ContentView.lastPostId);
    public const string QuantizationField = nameof(FileView.quantization);
    public const string DescriptionField = nameof(ModuleView.description);
    public const string InternalTypeStringField = nameof(ContentView.internalTypeString);

    //These fields are too difficult to modify with the attributes, so we do it in code here
    protected readonly Dictionary<(RequestType, string),string> StandardModifiedFields = new Dictionary<(RequestType, string), string> {
        { (RequestType.content, LastPostDateField), $"(select createDate from comments where {MainAlias}.id = contentId order by id desc limit 1) as {LastPostDateField}" },
        { (RequestType.content, LastPostIdField), $"(select id from comments where {MainAlias}.id = contentId order by id desc limit 1) as {LastPostIdField}" },
        { (RequestType.file, QuantizationField), $"(select value from content_values where {MainAlias}.id = contentId and key='{QuantizationField}' limit 1) as {QuantizationField}" },
        { (RequestType.module, DescriptionField), $"(select value from content_values where {MainAlias}.id = contentId and key='{DescriptionField}' limit 1) as {DescriptionField}" },
        { (RequestType.user, "registered"), $"(registrationKey IS NULL) as registered" }
    };

    //Some searches can easily be modified afterwards with constants, put that here.
    protected readonly Dictionary<RequestType, string> StandardSearchModifiers = new Dictionary<RequestType, string>()
    {
       { RequestType.file, $"internalType = {(int)InternalContentType.file}" },
       { RequestType.module, $"internalType = {(int)InternalContentType.module}" },
       { RequestType.page, $"internalType = {(int)InternalContentType.page}" },
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
        var lpdSelect = StandardModifiedFields[(RequestType.content, LastPostDateField)];
        StandardModifiedFields.Add((RequestType.file, LastPostDateField), lpdSelect);
        StandardModifiedFields.Add((RequestType.page, LastPostDateField), lpdSelect);
        StandardModifiedFields.Add((RequestType.module, LastPostDateField), lpdSelect);

        foreach(var type in ContentRequestTypes)
        {
            StandardModifiedFields.Add((type, InternalTypeStringField), EnumToCase<InternalContentType>(nameof(Db.Content.internalType), InternalTypeStringField));
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
    public List<SearchRequestPlus> RequestPreparse(SearchRequests requests)
    {
        var acceptedTypes = Enum.GetNames<RequestType>();
        var result = new List<SearchRequestPlus>();

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
            
            //Now do some pre-parsing and data retrieval. If one of these fail, it's good that it
            //fails early (although it will only fail if me, the programmer, did something wrong,
            //not if the user supplies a weird request)
            var reqplus = mapper.Map<SearchRequestPlus>(request);
            reqplus.requestType = Enum.Parse<RequestType>(reqplus.type); //I know this will succeed
            reqplus.typeInfo = typeService.GetTypeInfo(StandardViewRequests[reqplus.requestType]);
            reqplus.requestFields = ComputeRealFields(reqplus);
            result.Add(reqplus);
        }

        return result;
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
        if (StandardModifiedFields.ContainsKey((r.requestType, fieldName)))
            return StandardModifiedFields[(r.requestType, fieldName)];
        //Return whatever's in the fieldmap, EVEN if it's empty (if it's empty, we remove it from the selector)
        else if (r.typeInfo.fieldRemap.ContainsKey(fieldName))
            return r.typeInfo.fieldRemap[fieldName];
        //Just a basic fieldname replacement
        else
            return fieldName;
    }

    ///// <summary>
    ///// Returns whether or not the given field is CURRENTLY searchable within the given request context
    ///// </summary>
    ///// <param name="field"></param>
    ///// <param name="request"></param>
    ///// <returns></returns>
    //public bool StandardFieldCurrentlySearchable(string field, SearchRequestPlus request)
    //{
    //    //Don't even bother with fancy checks if this field isn't even searchable in
    //    //the first place! Context doesn't matter in that case.
    //    if(!request.typeInfo.searchableFields.Contains(field))
    //        return false;

    //    //Always searchable if it's a plain field
    //    if(!StandardModifiedFields.ContainsKey((request.requestType, field)) &&
    //        !request.typeInfo.fieldRemap.ContainsKey(field))
    //    {
    //        return true;
    //    }
    //    else
    //    {
    //        //If it's NOT a plain field, it MUST be included in the search results
    //        return request.requestFields.Contains(field);
    //    }
    //}

    public List<string> ComputeRealFields(SearchRequestPlus r)
    {
        var fields = r.fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); 

        //Redo fieldlist if they asked for special formats
        if (r.fields == "*")
            fields = new List<string>(r.typeInfo.queryableFields);
        
        //Check for bad fields
        foreach (var field in fields)
            if (!r.typeInfo.queryableFields.Contains(field))
                throw new ArgumentException($"Unknown field {field} in request {r.name}");
        
        return fields;
    }

    public string CombineQueryClause(string baseQuery, string clause)
    {
        if(string.IsNullOrWhiteSpace(baseQuery))
            return clause;
        else if (!string.IsNullOrWhiteSpace(clause))
            return $"({baseQuery}) AND ({clause})";
        return baseQuery; 
    }

    public void ThrowOnStandardFieldSearchableError(string field, SearchRequestPlus request)
    {
        if(!request.typeInfo.searchableFields.Contains(field))
            throw new ArgumentException($"Field '{field}' not searchable yet in type '{request.type}'({request.name})!");

        //Oops, this is a dangerous field, we can't just use it without requesting it because
        //it's COMPUTED
        if(StandardModifiedFields.ContainsKey((request.requestType, field)) || 
            request.typeInfo.fieldRemap.ContainsKey(field))
        {
            if(!request.requestFields.Contains(field))
                throw new ArgumentException($"Field '{field}' is a computed field for type '{request.type}' and must be included in the retrieved fieldlist ({request.name})");
        }
    }


    /// <summary>
    /// This method adds a 'standard' select for regular searches against simple
    /// single table queries. For instance, it might be "SELECT id,username FROM users "
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    public void AddStandardSelect(StringBuilder queryStr, SearchRequestPlus r)
    {
        var fieldSelect = r.requestFields.Select(x => StandardFieldRemap(x, r)).Where(x => !string.IsNullOrEmpty(x)).ToList();

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
                ThrowOnStandardFieldSearchableError(f, request);
                return f;
            }, v =>
            {
                var realValName = v.TrimStart('@');
                if (!parameters.ContainsKey(realValName))
                {
                    var dotParts = realValName.Split(".".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    var newName = realValName.Replace(".", "_");

                    if(dotParts.Length == 1)
                        throw new ArgumentException($"Value {v} not found for request {request.name}");

                    //For now, let's just assume when linking, they're going one deep
                    if(dotParts.Length != 2)
                        throw new ArgumentException($"For now, can only access 1 layer deep in results, fix {v} in {request.name}");

                    var resultName = dotParts[0];
                    var resultField = dotParts[1];

                    if(!parameters.ContainsKey(resultName))
                        throw new ArgumentException($"No base link {resultName} for {v} in {request.name}");
                    
                    var testList = parameters[resultName];

                    if(!(testList is IEnumerable<IDictionary<string, object>>))
                        throw new ArgumentException($"Base link {resultName} improper type for {v} in {request.name}");
                    
                    var valList = (IEnumerable<IDictionary<string, object>>)testList;

                    if(valList.Count() == 0)
                    {
                        //There's no results, so most things will fail, but whatever. In this case,
                        //we don't care about the field
                        parameters.Add(newName, new List<object>());
                        return $"@{newName}";
                    }

                    if(!valList.First().ContainsKey(resultField))
                        throw new ArgumentException($"Link result {resultName} has no field {resultField} for {v} in {request.name}");
                    
                    //OK we finally have it, let's go
                    parameters.Add(newName, valList.Select(x => x[resultField]));
                    return $"@{newName}";
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

    /// <summary>
    /// Assuming a query that can be limited simply, this adds the necessary LIMIT, OFFSET,
    /// and ORDER BY clauses. Most queries can use this function.
    /// </summary>
    /// <param name="queryStr"></param>
    /// <param name="r"></param>
    /// <param name="parameters"></param>
    public void AddStandardFinalLimit(StringBuilder queryStr, SearchRequestPlus r, Dictionary<string, object> parameters)
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
        var requestsPlus = RequestPreparse(requests);

        foreach(var request in requestsPlus)
        {
            queryStr.Clear();

            if(StandardViewRequests.ContainsKey(request.requestType))
            {
                //Generate "select from"
                AddStandardSelect(queryStr, request);                   

                //Generate "where (x)"
                var query = CreateStandardQuery(queryStr, request, modifiedValues);    
                query = CombineQueryClause(query, StandardSearchModifiers.GetValueOrDefault(request.requestType, ""));
                if (!string.IsNullOrWhiteSpace(query))
                    queryStr.Append($"WHERE {query} ");

                //Generate "order by limit offset"
                AddStandardFinalLimit(queryStr, request, modifiedValues);     
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