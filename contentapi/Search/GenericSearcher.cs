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

////This should probably move out at some point
//public class SearchRequestPlus : SearchRequest
//{
//    public Db.User requester {get;set;} = new Db.User();
//    public RequestType requestType {get;set;}
//    public TypeInfo typeInfo {get;set;} = new TypeInfo();
//    public List<string> requestFields {get;set;} = new List<string>();
//    public Guid requestId = Guid.NewGuid();
//    public Guid globalRequestId {get;set;} = Guid.Empty;
//    public string UniqueRequestKey(string field)
//    {
//        return $"_req{requestId.ToString().Replace("-", "")}_{name}_{field}";
//    }
//    public string GlobalRequestKey(string field)
//    {
//        return $"_req{globalRequestId.ToString().Replace("-", "")}_{field}";
//    }
//    public string RequesterKey()
//    {
//        return GlobalRequestKey("requesterID");
//    }
//}

public class GenericSearcher : IGenericSearch
{
    protected ILogger logger;
    protected IDbConnection dbcon;
    protected ITypeInfoService typeService;
    protected GenericSearcherConfig config;
    protected ISearchQueryParser parser;
    protected IMapper mapper;
    protected IQueryBuilder queryBuilder;

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, GenericSearcherConfig config, IMapper mapper,
        ISearchQueryParser parser, IQueryBuilder queryBuilder)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
        this.config = config;
        this.mapper = mapper;
        this.parser = parser;
        this.queryBuilder = queryBuilder;
    }

    ///// <summary>
    ///// Throw exceptions on any funky business we can quickly report
    ///// </summary>
    ///// <param name="requests"></param>
    //public async Task<List<SearchRequestPlus>> RequestPreparseAsync(SearchRequests requests, long requestUserId)
    //{
    //    var acceptedTypes = Enum.GetNames<RequestType>();
    //    var result = new List<SearchRequestPlus>();
    //    var globalId = Guid.NewGuid();
    //    Db.User requester = new Db.User() //This is a default user, make SURE all the relevant fields are set!
    //    {
    //        id = 0,
    //        super = false
    //    };

    //    //Do a (hopefully) quick lookup for the request user!
    //    if(requestUserId > 0)
    //    {
    //        try
    //        {
    //            //This apparently throws an exception if it fails
    //            requester = await dbcon.QuerySingleAsync<User>("select * from users where id = @requestUserId", 
    //                new { requestUserId = requestUserId });
    //        }
    //        catch(Exception ex)
    //        {
    //            logger.LogWarning($"Error while looking up requester: {ex}");
    //            throw new ArgumentException($"Unknown request user {requestUserId}");
    //        }
    //    }

    //    foreach(var request in requests.requests)
    //    {
    //        //Oops, unknown type
    //        if(!acceptedTypes.Contains(request.type))
    //            throw new ArgumentException($"Unknown request type: {request.type} in request {request.name}");
    //        
    //        //Users HAVE to name their stuff! Otherwise the output of the data
    //        //dictionary won't make any sense! Note that the method for this check 
    //        //isn't EXACTLY right but... it should be fine, fields are all
    //        //just regular identifiers, like in a programming language.
    //        if(!parser.IsFieldNameValid(request.name))
    //            throw new ArgumentException($"Malformed name '{request.name}'");

    //        if(requests.values.ContainsKey(request.name))
    //            throw new ArgumentException($"Key/name collision: request named {request.name}, which is also provided value");

    //        if(requests.requests.Count(x => x.name == request.name) > 1)
    //            throw new ArgumentException($"Name collision: request named {request.name} shows up twice!");
    //        
    //        //Now do some pre-parsing and data retrieval. If one of these fail, it's good that it
    //        //fails early (although it will only fail if me, the programmer, did something wrong,
    //        //not if the user supplies a weird request)
    //        var reqplus = mapper.Map<SearchRequestPlus>(request);
    //        reqplus.requestType = Enum.Parse<RequestType>(reqplus.type); //I know this will succeed
    //        reqplus.typeInfo = typeService.GetTypeInfo(StandardViewRequests[reqplus.requestType]);
    //        reqplus.requestFields = ComputeRealFields(reqplus);
    //        reqplus.globalRequestId = globalId;
    //        reqplus.requester = requester;
    //        result.Add(reqplus);
    //    }

    //    return result;
    //}

    public async Task<IEnumerable<IDictionary<string, object>>> QueryAsyncCast(string query, object parameters)
    {
        return (await dbcon.QueryAsync(query, parameters)).Cast<IDictionary<string, object>>();
    }

    public async Task AddExtraFields(SearchRequestPlus r, IEnumerable<IDictionary<string, object>> result)
    {
        if(queryBuilder.ContentRequestTypes.Contains(r.requestType))
        {
            const string keykey = nameof(ContentView.keywords);
            const string valkey = nameof(ContentView.values);
            const string permkey = nameof(ContentView.permissions);
            const string votekey = nameof(ContentView.votes);
            const string cidkey = nameof(Db.ContentKeyword.contentId); //WARN: assuming it's the same for all!
            var ids = result.Select(x => x["id"]);

            if(r.requestFields.Contains(keykey))
            {
                var keyinfo = typeService.GetTypeInfo<Db.ContentKeyword>();
                var keywords = await QueryAsyncCast($"select {cidkey},value from {keyinfo.database} where {cidkey} in @ids",
                    new { ids = ids });

                foreach(var c in result)
                    c[keykey] = keywords.Where(x => x[cidkey].Equals(c["id"])).Select(x => x["value"]).ToList();
            }
            if(r.requestFields.Contains(valkey))
            {
                var valinfo = typeService.GetTypeInfo<Db.ContentValue>();
                var values = await QueryAsyncCast($"select {cidkey},key,value from {valinfo.database} where {cidkey} in @ids",
                    new { ids = ids });

                foreach(var c in result)
                    c[valkey] = values.Where(x => x[cidkey].Equals(c["id"])).ToDictionary(x => x["key"], y => y["value"]);
            }
            if(r.requestFields.Contains(permkey))
            {
                var perminfo = typeService.GetTypeInfo<Db.ContentPermission>();
                var permissions = await dbcon.QueryAsync($"select * from {perminfo.database} where {cidkey} in @ids",
                    new { ids = ids });

                foreach(var c in result)
                {
                    //TODO: May need to move this conversion somewhere else... not sure
                    c[permkey] = permissions.Where(x => x.contentId.Equals(c["id"])).ToDictionary(
                        x => x.userId, y => $"{(y.create==1?"C":"")}{(y.read==1?"R":"")}{(y.update==1?"U":"")}{(y.delete==1?"D":"")}");
                }
            }
            if(r.requestFields.Contains(votekey))
            {
                var voteinfo = typeService.GetTypeInfo<Db.ContentVote>();
                var votes = await dbcon.QueryAsync($"select {cidkey}, vote, count(*) as count from {voteinfo.database} where {cidkey} in @ids group by {cidkey}, vote",
                    new { ids = ids });
                var displayVotes = Enum.GetValues<VoteType>().Where(x => x != VoteType.none).Select(x => x.ToString());

                foreach(var c in result)
                {
                    var cvotes = votes.Where(x => x.contentId.Equals(c["id"])).ToDictionary(x => ((VoteType)x.vote).ToString(), y => y.count);
                    foreach(var v in displayVotes)
                    {
                        if(!cvotes.ContainsKey(v))
                            cvotes.Add(v, 0);
                    }
                    c[votekey] = cvotes;
                }
            }
        }
    }

    //A basic search doesn't have a concept of a "request user" or permissions, those are set up
    //prior to this. This function just wants to build a query based on what you give it
    protected async Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> SearchBase(
        List<SearchRequest> requests, Dictionary<string, object> parameterValues)
    {
        var result = new Dictionary<string, IEnumerable<IDictionary<string, object>>>();

        //Nothing to do!
        if(requests.Count == 0)
            return result;

        foreach(var request in requests)
        {
            //Need to limit the 'limit'!
            request.limit = Math.Min(request.limit > 0 ? request.limit : int.MaxValue, config.MaxIndividualResultSet);
            var reqplus = queryBuilder.FullParseRequest(request, parameterValues);
            logger.LogDebug($"Running SQL for {request.type}({request.name}): {reqplus.computedSql}");

            var dp = new DynamicParameters(parameterValues);
            var qresult = await QueryAsyncCast(reqplus.computedSql, dp);

            //Just because we got the qresult doesn't mean we can stop! if it's content, we need
            //to fill in the values, keywords, and permissions!
            await AddExtraFields(reqplus, qresult);

            //Add the results to the USER results, AND add it to our list of values so it can
            //be used in chaining. It's just a reference, don't worry about duplication or whatever.
            result.Add(request.name, qresult);
            parameterValues.Add(request.name, qresult);
        }

        return result;
    }

    //A restricted search doesn't allow you to retrieve results that the given request user can't read
    public async Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> SearchRestricted(
        SearchRequests requests, long requestUserId = 0)
    {
        var globalId = Guid.NewGuid();
        Db.User requester = new Db.User() //This is a default user, make SURE all the relevant fields are set!
        {
            id = 0,
            super = false
        };

        //Do a (hopefully) quick lookup for the request user!
        if(requestUserId > 0)
        {
            try
            {
                //This apparently throws an exception if it fails
                requester = await dbcon.QuerySingleAsync<User>("select * from users where id = @requestUserId", 
                    new { requestUserId = requestUserId });
            }
            catch(Exception ex)
            {
                logger.LogWarning($"Error while looking up requester: {ex}");
                throw new ArgumentException($"Unknown request user {requestUserId}");
            }
        }

        //Need to add requester key to parameter list!
        var requesterKey = $"_sys{globalId.ToString().Replace("-", "")}_requester";
        var parameterValues = new Dictionary<string, object>(requests.values);
        parameterValues.Add(requesterKey, requestUserId);

        //Modify the queries before giving them out to the query builder! We NEED them
        //to be absolutely restricted by permissions!
        foreach(var request in requests.requests)
        {
            //This is VERY important: limit content searches based on permissions!
            if(queryBuilder.ContentRequestTypes.Select(x => x.ToString()).Contains(request.type))
                request.query = queryBuilder.CombineQueryClause(request.query, $"!permissionlimit(@{requesterKey}, id)");
            else if(request.type == "comment" || request.type == "activity") //ALSO MODULEMESSAGE!@!
                request.query = queryBuilder.CombineQueryClause(request.query, $"!permissionlimit(@{requesterKey}, contentId)");
        }

        return await SearchBase(requests.requests, parameterValues);
    }

    //This search is a plain search, no permission limits or user lookups.
    public async Task<Dictionary<string, IEnumerable<IDictionary<string, object>>>> Search(SearchRequests requests)
    {
        return await SearchBase(requests.requests, new Dictionary<string, object>(requests.values));
    }

    public List<T> ToStronglyTyped<T>(IEnumerable<IDictionary<string, object>> singleResults)
    {
        return singleResults.Select(x => mapper.Map<T>(x)).ToList();
    }
}