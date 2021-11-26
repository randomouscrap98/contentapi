using System.Data;
using System.Text;
using System.Text.RegularExpressions;
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
    public string NameRegex {get;set;} = "^[a-zA-Z_][a-zA-Z0-9_]*$";
    public string ParameterRegex {get;set;} = "^@[a-zA-Z_][a-zA-Z0-9_\\.]*$";
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
        { RequestType.content, typeof(ContentView) }
    };

    protected readonly Dictionary<(RequestType, string),string> ModifiedFields = new Dictionary<(RequestType, string), string> {
        { (RequestType.content, "lastPostDate"), $"(select createDate from comments where {MainAlias}.id = contentId order by id desc limit 1) as lastPostDate" },
        { (RequestType.user, "registered"), $"(registrationKey IS NULL) as registered" }
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
    }

    public string SystemKey(SearchRequestPlus request, string field)
    {
        return $"{SystemPrepend}_{request.name}_{field}";
    }

    //Throw exceptions on any funky business we can quickly report
    public void RequestPrecheck(SearchRequests requests)
    {
        var acceptedTypes = Enum.GetNames<RequestType>();

        foreach(var request in requests.requests)
        {
            //Oops, unknown type
            if(!acceptedTypes.Contains(request.type))
                throw new ArgumentException($"Unknown request type: {request.type} in request {request.name}");
            
            //Oops, please name your requests appropriately for linking
            if(!Regex.IsMatch(request.name, config.NameRegex))
                throw new ArgumentException($"Malformed name {request.name}, must be {config.NameRegex}");
        }
    }

    //MOST fields should work with this function, either it's the same name, it's a simple
    //remap, or the remap is complex and we need a specialized modified field.
    public string StandardFieldRemap(string fieldName, SearchRequestPlus r)
    {
        //var modifiedTuple = Tuple.Create(r.requestType, fieldName);

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
        queryStr.Append($"LIMIT @{limitKey} ");//{limit} ");
        parameters.Add(limitKey, limit);

        if (r.skip > 0)
        {
            var skipKey = SystemKey(r, "skip");
            queryStr.Append($"OFFSET @{skipKey} ");
            parameters.Add(skipKey, limit);
        }
    }

    //All searches are reads, don't need to open the connection OR set up transactions, wow.
    public async Task<Dictionary<string, object>> Search(SearchRequests requests)
    {
        var result = new Dictionary<string, object>();
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
                //Generates the "select from"
                AddStandardSelect(queryStr, request);
            }
            else
            {
                throw new NotImplementedException($"Sorry, {request.type} isn't ready yet!");
            }

            //Not sure if the "query" is generic but... mmm maybe
            try
            {
                var parseResult = parser.ParseQuery(request.query, f =>
                {
                    if(request.typeInfo.queryableFields.Contains(f))
                        return f;
                    else
                        throw new ArgumentException($"No field {f} in type {request.type}({request.name})!");
                }, v =>
                {
                    var realValName = v.TrimStart('@');
                    if(!modifiedValues.ContainsKey(realValName))
                    {
                        //There are two options: one is that it's just deeper in, the other
                        //is that it just doesn't exist. Keep going down and down through dot
                        //operators until we reach the actual value, and if it exists, add
                        //it with the full path (dots converted to underscores) to the values list
                        throw new ArgumentException($"Unknown value {v} in {request.name}");
                    }
                    return v;
                });

                if(!string.IsNullOrWhiteSpace(parseResult))
                    queryStr.Append($"WHERE {parseResult} ");
            }
            catch(Exception ex)
            {
                //Convert to argument exception so the user knows what's up
                logger.LogWarning($"Exception during query parse: {ex}");
                throw new ArgumentException(ex.Message);
            }

            //Add the order and limit and whatever
            BasicFinalLimit(queryStr, request, modifiedValues);

            var sql = queryStr.ToString();
            logger.LogDebug($"Running SQL for {request.type}({request.name}): {sql}");

            var dp = new DynamicParameters(modifiedValues);
            var qresult = await dbcon.QueryAsync(sql, dp);

            //Add the results to the USER results, AND add it to our list of values so it can
            //be used in chaining. It's just a reference, don't worry about duplication or whatever.
            result.Add(request.name, qresult);
            modifiedValues.Add(request.name, qresult);
        }

        return result;
    }
}