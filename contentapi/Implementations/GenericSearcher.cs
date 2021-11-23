using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using contentapi.Db;
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
    protected readonly List<RequestType> StandardSelect = new List<RequestType> {
        RequestType.user,
        RequestType.comment,
        RequestType.content
    };

    protected readonly Dictionary<RequestType, List<string>> ModifiedFields = new Dictionary<RequestType, List<string>>() {
        { RequestType.content, new List<string>() { }}
    };

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, GenericSearcherConfig config)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
        this.config = config;
    }

    //All searches are reads, don't need to open the connection OR set up transactions, wow.
    public async Task<Dictionary<string, object>> Search(SearchRequests requests)
    {
        var result = new Dictionary<string, object>();
        var modifiedValues = new Dictionary<string, object>(requests.values);
        var queryStr = new StringBuilder();
        var acceptedTypes = Enum.GetNames<RequestType>();

        //Before wasting the user's time on useless junk, check some simple stuff
        //Might look silly to do this loop twice but whatever, be nice to the users
        foreach(var request in requests.requests)
        {
            //Oops, unknown type
            if(!acceptedTypes.Contains(request.type))
                throw new ArgumentException($"Unknown request type: {request.type}");
            
            //Oops, please name your requests appropriately for linking
            if(!Regex.IsMatch(request.name, config.NameRegex))
                throw new ArgumentException($"Malformed name {request.name}, must be {config.NameRegex}");
        }

        foreach(var request in requests.requests)
        {
            queryStr.Clear();
            var dp = new DynamicParameters(modifiedValues);
        }

        return result;
    }
}