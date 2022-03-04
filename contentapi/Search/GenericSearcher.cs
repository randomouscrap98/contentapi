using System.Data;
using System.Diagnostics;
using AutoMapper;
using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;
using Newtonsoft.Json;
using QueryResult = System.Collections.Generic.IDictionary<string, object>;
using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.Search;

/// <summary>
/// Configuration for the generic searcher
/// </summary>
public class GenericSearcherConfig
{
    public int MaxIndividualResultSet {get;set;} = 1000;
}

//Note to self: ALL query limiters NEED to go in THIS class, all together!
//Any permission limits, count limits, etc. The "QueryBuilder" needs to allow
//ANY query to go through, so it is useful in far more contexts.
public class GenericSearcher : IGenericSearch
{
    protected ILogger logger;
    protected IDbConnection dbcon;
    protected IPermissionService permissionService;
    protected IViewTypeInfoService typeService;
    protected GenericSearcherConfig config;
    protected IMapper mapper;
    protected IQueryBuilder queryBuilder;

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        IViewTypeInfoService typeInfoService, GenericSearcherConfig config, IMapper mapper,
        IQueryBuilder queryBuilder, IPermissionService permissionService)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
        this.config = config;
        this.mapper = mapper;
        this.queryBuilder = queryBuilder;
        this.permissionService = permissionService;
    }

    public async Task<QueryResultSet> QueryAsyncCast(string query, object parameters)
    {
        return (await dbcon.QueryAsync(query, parameters)).Cast<IDictionary<string, object>>();
    }

    public IEnumerable<object> GetIds(QueryResultSet result) => result.Select(x => x["id"]).OrderBy(x => x);
    public Dictionary<long, QueryResult> IndexResults(QueryResultSet result) => result.ToDictionary(x => (long)x["id"], y => y);

    //WARN: should this be part of query builder?? who knows... it kinda doesn't need to be, it's not a big deal.
    public async Task AddExtraFields(SearchRequestPlus r, QueryResultSet result)
    {
        //You CANNOT index results outside the if statements, since they might by default not have an id field!

        //This adds groups to users (if requested)
        if(r.requestType == RequestType.user)
        {
            const string groupskey = nameof(UserView.groups);
            const string ridkey =  nameof(Db.UserRelation.relatedId);
            const string uidkey =  nameof(Db.UserRelation.userId);
            const string typekey = nameof(Db.UserRelation.type);
            var index = IndexResults(result);

            if(r.requestFields.Contains(groupskey))
            {
                var relinfo = typeService.GetTypeInfo<Db.UserRelation>();
                var groups = await dbcon.QueryAsync<Db.UserRelation>($"select {ridkey},{uidkey} from {relinfo.selfDbInfo?.modelTable} where {typekey} = @type and {uidkey} in @ids",
                    new { ids = index.Keys, type = (int)UserRelationType.inGroup }); 
                
                var lookup = groups.ToLookup(x => x.userId);

                foreach(var u in index)
                    u.Value[groupskey] = lookup.Contains(u.Key) ? lookup[u.Key].Select(x => x.relatedId).ToList() : new List<long>();
            }
        }

        if(r.requestType == RequestType.message)
        {
            const string cidkey = nameof(Db.MessageValue.messageId);
            const string valkey = nameof(MessageView.values);
            var index = IndexResults(result);

            if(r.requestFields.Contains(valkey))
            {
                var valinfo = typeService.GetTypeInfo<Db.MessageValue>();
                var values = await dbcon.QueryAsync<Db.MessageValue>($"select {cidkey},key,value from {valinfo.selfDbInfo?.modelTable} where {cidkey} in @ids",
                    new { ids = index.Keys });

                var lookup = values.ToLookup(x => x.messageId);

                foreach(var c in index) 
                    c.Value[valkey] = lookup.Contains(c.Key) ? lookup[c.Key].ToDictionary(x => x.key, y => JsonConvert.DeserializeObject(y.value)) : new Dictionary<string, object?>();
            }
        }

        if(r.requestType == RequestType.content) //queryBuilder.ContentRequestTypes.Contains(r.requestType))
        {
            const string keykey = nameof(ContentView.keywords);
            const string valkey = nameof(ContentView.values);
            const string permkey = nameof(ContentView.permissions);
            const string votekey = nameof(ContentView.votes);
            //const string yourvotekey = nameof(ContentView.yourVote);
            const string cidkey = nameof(Db.ContentKeyword.contentId); //WARN: assuming it's the same for all!
            var index = IndexResults(result);
            var ids = index.Keys.ToList();
            
            var voteinfo = typeService.GetTypeInfo<Db.ContentVote>();

            if(r.requestFields.Contains(keykey))
            {
                var keyinfo = typeService.GetTypeInfo<Db.ContentKeyword>();
                var keywords = await dbcon.QueryAsync<Db.ContentKeyword>($"select {cidkey},value from {keyinfo.selfDbInfo?.modelTable} where {cidkey} in @ids",
                    new { ids = ids });

                var lookup = keywords.ToLookup(x => x.contentId);

                foreach(var c in index)
                    c.Value[keykey] = lookup.Contains(c.Key) ? lookup[c.Key].Select(x => x.value).ToList() : new List<string>();
            }
            if(r.requestFields.Contains(valkey))
            {
                var valinfo = typeService.GetTypeInfo<Db.ContentValue>();
                var values = await dbcon.QueryAsync<Db.ContentValue>($"select {cidkey},key,value from {valinfo.selfDbInfo?.modelTable} where {cidkey} in @ids",
                    new { ids = ids });

                var lookup = values.ToLookup(x => x.contentId);

                foreach(var c in index)
                    c.Value[valkey] = lookup.Contains(c.Key) ? lookup[c.Key].ToDictionary(x => x.key, y => JsonConvert.DeserializeObject(y.value)) : new Dictionary<string, object?>();
            }
            if(r.requestFields.Contains(permkey))
            {
                var perminfo = typeService.GetTypeInfo<Db.ContentPermission>();
                var permissions = await dbcon.QueryAsync($"select * from {perminfo.selfDbInfo?.modelTable} where {cidkey} in @ids",
                    new { ids = ids });

                var lookup = permissions.ToLookup(x => x.contentId);

                foreach(var c in index)
                    c.Value[permkey] = permissionService.ResultToPermissions(lookup.Contains(c.Key) ? lookup[c.Key] : new List<dynamic>());//permissions.Where(x => x.contentId.Equals(c["id"])));
            }
            if(r.requestFields.Contains(votekey))
            {
                var votes = await dbcon.QueryAsync($"select {cidkey}, vote, count(*) as count from {voteinfo.selfDbInfo?.modelTable} where {cidkey} in @ids group by {cidkey}, vote",
                    new { ids = ids });
                var displayVotes = Enum.GetValues<VoteType>().Where(x => x != VoteType.none); 

                var lookup = votes.ToLookup(x => x.contentId);

                foreach(var c in index)
                {
                    var cvotes = lookup.Contains(c.Key) ? lookup[c.Key].ToDictionary(x => (VoteType)x.vote, y => y.count) : new Dictionary<VoteType, dynamic>();
                    foreach(var v in displayVotes)
                    {
                        if(!cvotes.ContainsKey(v))
                            cvotes.Add(v, 0);
                    }
                    c.Value[votekey] = cvotes;
                }
            }
            //if(r.requestFields.Contains(yourvotekey))
            //{
            //    var yourVote = await dbcon.QueryAsync($"select {cidkey}, vote from {voteinfo.selfDbInfo?.modelTable} where {cidkey} in @ids and userId = @uid",
            //        new { ids = ids, uid = r.}
            //}
        }
    }

    public Task<QueryResultSet> QueryRawAsync(string sql, Dictionary<string, object> values)
    {
        var dp = new DynamicParameters(values);
        return QueryAsyncCast(sql, dp);
    }

    public string GetDatabaseForType<T>()
    {
        var typeinfo = typeService.GetTypeInfo<T>();
        //return typeinfo.modelTable ?? throw new InvalidOperationException($"No database for type {typeof(T).Name}");
        return typeinfo.selectFromSql ?? throw new InvalidOperationException($"No database for type {typeof(T).Name}");
    }    

    //The simple method for performing a SINGLE request as given. The database accesses are timed...
    protected async Task<QueryResultSet> SearchSingle(
        SearchRequest request, 
        Dictionary<string, object> parameterValues, 
        Dictionary<string, double>? timedic = null)
    {
        //Need to limit the 'limit'!
        var reqplus = queryBuilder.FullParseRequest(request, parameterValues);
        logger.LogDebug($"Running SQL for {request.type}({request.name}): {reqplus.computedSql}");

        //Warn: we repeatedly do this because the FullParseRequest CAN modify parameter values
        var dp = new DynamicParameters(parameterValues);

        var timer = new Stopwatch();

        //To give it a fighting chance, here's the MOST RAW I can do.
        //timer.Start();
        //var whatever = dbcon.Execute(reqplus.computedSql, dp);
        //timer.Stop();
        //timedic?.Add(request.name + "_syncraw", timer.Elapsed.TotalMilliseconds);

        timer.Restart();
        var qresult = await QueryAsyncCast(reqplus.computedSql, dp);
        timer.Stop();

        timedic?.Add(request.name, timer.Elapsed.TotalMilliseconds);

        //We also want to time extra fields
        timer.Restart();
        //Just because we got the qresult doesn't mean we can stop! if it's content, we need
        //to fill in the values, keywords, and permissions!
        await AddExtraFields(reqplus, qresult);
        timer.Stop();

        timedic?.Add($"{request.name}_extras", timer.Elapsed.TotalMilliseconds);

        return qresult;
    }

    //A basic search doesn't have a concept of a "request user" or permissions, those are set up
    //prior to this. This function just wants to build a query based on what you give it
    protected async Task<GenericSearchResult> SearchBase(
        SearchRequests requests, Dictionary<string, object> parameterValues)
    {
        var result = new GenericSearchResult() { search = requests };

        //Nothing to do!
        if(requests.requests.Count == 0)
            return result;
        
        //Some nice prechecks
        foreach(var request in requests.requests)
        {
            if(string.IsNullOrWhiteSpace(request.name))
                request.name = request.type;

            if(requests.requests.Count(x => x.name == request.name) > 1)
                throw new ArgumentException($"Duplicate name {request.name} in requests! Consider using 'name' to differentiate");
        }

        foreach(var request in requests.requests)
        {
            var qresult = await SearchSingle(request, parameterValues, result.databaseTimes);

            //Add the results to the USER results, AND add it to our list of values so it can
            //be used in chaining. It's just a reference, don't worry about duplication or whatever.
            result.data.Add(request.name, qresult);
            parameterValues.Add(request.name, qresult);
        }

        return result;
    }

    public async Task<T> GetById<T>(RequestType type, long id, bool throwIfDeleted = false)
    {
        var values = new Dictionary<string, object>();
        values.Add("id", id);

        var result = await SearchSingle(new SearchRequest() { 
            name = "searchById", 
            type = type.ToString(), 
            fields = "*",
            query = "id = @id"
        }, values);

        if(result.Count() != 1 || (throwIfDeleted && result.First().ContainsKey("deleted") && (long)result.First()["deleted"] != 0))
            throw new NotFoundException($"{type} with ID {id} not found!"); 

        return ToStronglyTyped<T>(result).First();
    }

    public async Task<List<T>> GetByField<T>(RequestType type, string fieldname, object value, string comparator = "=")
    {
        var values = new Dictionary<string, object>();
        values.Add("value", value);

        var result = await SearchSingle(new SearchRequest() { 
            name = "searchByField", 
            type = type.ToString(), 
            fields = "*",
            query = $"{fieldname} {comparator} @value"
        }, values);

        return ToStronglyTyped<T>(result);
    }

    //A restricted search doesn't allow you to retrieve results that the given request user can't read
    public async Task<GenericSearchResult> Search(SearchRequests requests, long requestUserId = 0)
    {
        var globalId = Guid.NewGuid();
        UserView requester = new UserView() //This is a default user, make SURE all the relevant fields are set!
        {
            id = 0,
            super = false,
            groups = new List<long>()
        };

        //Do a (hopefully) quick lookup for the request user!
        if(requestUserId > 0)
        {
            try
            {
                //This apparently throws an exception if it fails
                requester = await GetById<UserView>(RequestType.user, requestUserId);
            }
            catch(Exception ex)
            {
                logger.LogWarning($"Error while looking up requester: {ex}");
                throw new ArgumentException($"Unknown request user {requestUserId}");
            }
        }

        //Need to add requester key to parameter list!
        var globalPre = $"_sys{globalId.ToString().Replace("-", "")}";
        var requesterKey = $"{globalPre}_requester";
        var groupsKey = $"{globalPre}_groups";
        var parameterValues = new Dictionary<string, object>(requests.values);
        parameterValues.Add(requesterKey, requestUserId);
        parameterValues.Add(groupsKey, permissionService.GetPermissionIdsForUser(requester));

        //Modify the queries before giving them out to the query builder! We NEED them
        //to be absolutely restricted by permissions!
        foreach(var request in requests.requests)
        {
            //This USED to be part of the single search thing, but I want unrestricted search to be
            //truly unrestricted
            request.limit = Math.Min(request.limit > 0 ? request.limit : int.MaxValue, config.MaxIndividualResultSet);

            //This is VERY important: limit content searches based on permissions!
            if(request.type == RequestType.content.ToString()) //queryBuilder.ContentRequestTypes.Select(x => x.ToString()).Contains(request.type))
            {
                request.query = queryBuilder.CombineQueryClause(request.query, $"!permissionlimit(@{groupsKey}, id, R)");
            }
            if(request.type == RequestType.message.ToString() || request.type == RequestType.activity.ToString() || 
               request.type == RequestType.watch.ToString() || request.type == RequestType.vote.ToString())
            {
                request.query = queryBuilder.CombineQueryClause(request.query, $"!permissionlimit(@{groupsKey}, contentId, R)");
            }
            if(request.type == RequestType.message.ToString())
            {
                request.query = queryBuilder.CombineQueryClause(request.query, $"!receiveuserlimit(@{requesterKey})");
            }
            //Watches and variables and votes are per-user!
            if(request.type == RequestType.watch.ToString() || request.type == RequestType.uservariable.ToString() ||
               request.type == RequestType.vote.ToString())
            {
                request.query = queryBuilder.CombineQueryClause(request.query, $"userId = @{requesterKey}");
            }
            if(request.type == RequestType.adminlog.ToString())
            {
                if(!requester.super)
                    throw new ForbiddenException("You must be super to access the admin logs!");
            }
        }

        return await SearchBase(requests, parameterValues);
    }


    //This search is a plain search, no permission limits or user lookups.
    public async Task<GenericSearchResult> SearchUnrestricted(SearchRequests requests)
    {
        return await SearchBase(requests, new Dictionary<string, object>(requests.values));
    }

    public List<T> ToStronglyTyped<T>(QueryResultSet singleResults)
    {
        return singleResults.Select(x => mapper.Map<T>(x)).ToList();
    }
}