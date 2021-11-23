using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using contentapi.Db;
using contentapi.Views;
using Dapper;

namespace contentapi.Implementations;

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
    public string NameRegex {get;set;} = "^[a-zA-Z0-9_]+$";
    public string ParameterRegex {get;set;} = "^@[a-zA-Z0-9_\\.]+$";
}

public class GenericSearcher : IGenericSearch
{
    protected ILogger logger;
    protected IDbConnection dbcon;
    protected ITypeInfoService typeService;
    protected GenericSearcherConfig config;

    //Should this be configurable? I don't care for now
    protected readonly Dictionary<RequestType, Type> StandardSelect = new Dictionary<RequestType, Type> {
        { RequestType.user, typeof(UserView) },
        { RequestType.comment, typeof(CommentView) },
        { RequestType.content, typeof(ContentView) }
    };

    protected readonly Dictionary<Tuple<RequestType, string>,string> ModifiedFields = new Dictionary<Tuple<RequestType, string>, string> {
        { Tuple.Create(RequestType.content, "lastPostDate"), "(select createDate from comments where main.id = contentId order by id desc limit 1) as lastPostDate" }
    };

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, GenericSearcherConfig config)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
        this.config = config;
    }

    //Throw exceptions on any funky business we can quickly report
    public void RequestPrecheck(SearchRequests requests)
    {
        var acceptedTypes = Enum.GetNames<RequestType>();

        foreach(var request in requests.requests)
        {
            //Oops, unknown type
            if(!acceptedTypes.Contains(request.type))
                throw new ArgumentException($"Unknown request type: {request.type}");
            
            //Oops, please name your requests appropriately for linking
            if(!Regex.IsMatch(request.name, config.NameRegex))
                throw new ArgumentException($"Malformed name {request.name}, must be {config.NameRegex}");
        }
    }

    //MOST fields should work with this function, either it's the same name, it's a simple
    //remap, or the remap is complex and we need a specialized modified field.
    public string StandardFieldRemap(string fieldName, RequestType reqType, TypeInfo td)
    {
        if (td.fieldRemap.ContainsKey(fieldName))
        {
            var remap = td.fieldRemap[fieldName];
            var modifiedTuple = Tuple.Create(reqType, fieldName);

            if (!string.IsNullOrEmpty(remap))
                return remap;
            else if (ModifiedFields.ContainsKey(modifiedTuple))
                return ModifiedFields[modifiedTuple];
            else
                    throw new InvalidOperationException($"No field handler for {reqType}.{fieldName}");
        }
        else
        {
            return fieldName;
        }
    }

    public void AddStandardSelect(StringBuilder queryStr, SearchRequest request, RequestType reqType)
    {
        var td = typeService.GetTypeInfo(StandardSelect[reqType]);
        var fields = request.fields.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); 

        //Redo fieldlist if they asked for special formats
        if (request.fields == "*")
            fields = new List<string>(td.queryableFields);
        
        //Check for bad fields
        foreach (var field in fields)
            if (!td.queryableFields.Contains(field))
                throw new ArgumentException($"Unknown field {field} in request {request.name}");

        var fieldSelect = fields.Select(x => StandardFieldRemap(x, reqType, td)).ToList();

        queryStr.Append("SELECT ");
        queryStr.Append(string.Join(",", fieldSelect));
        queryStr.Append(" FROM ");
        queryStr.Append(td.database ?? throw new InvalidOperationException($"Standard select {request.type} doesn't map to database table!"));
        queryStr.Append(" ");
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

        foreach(var request in requests.requests)
        {
            var reqType = Enum.Parse<RequestType>(request.type); //I know this will succeed
            queryStr.Clear();

            if(StandardSelect.ContainsKey(reqType))
            {
                AddStandardSelect(queryStr, request, reqType);
            }
            else
            {
                throw new NotImplementedException($"Sorry, {request.type} isn't ready yet!");
            }
            var dp = new DynamicParameters(modifiedValues);
        }

        return result;
    }
}