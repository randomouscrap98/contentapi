using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    /// <summary>
    /// All services required to "run" the chainer
    /// </summary>
    public class ChainServices
    {
        public FileViewService file {get;set;}
        public UserViewService user {get;set;}
        public ContentViewService content {get;set;}
        public CategoryViewService category {get;set;}
        public CommentViewService comment {get;set;}
        public ActivityViewService activity {get;set;}
        public WatchViewService watch {get;set;}
        public VoteViewService vote {get;set;}
    }

    /// <summary>
    /// A chain result tagged with an ID for merging. Two chain results are the same if their 
    /// ids are the same within a given "type list"
    /// </summary>
    public class TaggedChainResult
    {
        public ExpandoObject result {get;set;}
        public long id {get;set;}
    }

    /// <summary>
    /// The basic data in every chain request. You always have to know what you want and where you want to put it
    /// </summary>
    public class ChainRequestBase
    {
        public object mergeLock {get;set;}                      // Set this appropriately! Everyone should use the same lock!
        public List<TaggedChainResult> mergeList {get;set;}     // Where to put the results!
        public List<string> fields {get;set;}                   // Which fields you want in the results
        public IEnumerable<Chaining> chains {get;set;}          // Description of what you want linked into search
        public string name {get;set;}                           // A special name to place this thing

        public ChainRequestBase() {}

        //This SHOULD be done with the mapper but I don't want the dependency
        public ChainRequestBase(ChainRequestBase copy)
        {
            mergeLock = copy.mergeLock; //Is this safe? Is it the "same" object?
            mergeList = copy.mergeList;
            fields = copy.fields;
            chains = copy.chains;
        }
    }

    /// <summary>
    /// Represents a single chain request, which may include multiple references (chainings)
    /// to previous results.
    /// </summary>
    /// <typeparam name="S"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class ChainRequest<S, V> : ChainRequestBase where V : IIdView where S : IIdSearcher
    {
        public S baseSearch {get;set;}                          // The search BEFORE adding chaining ids
        public Func<S, Task<List<V>>> retriever {get;set;}      // How you want us to get the results

        public ChainRequest() {}
        public ChainRequest(ChainRequestBase copy) : base(copy) {}
    }

    /// <summary>
    /// Represents a single chain request using the special syntax system. Note: the syntax may be 
    /// unreliable and change frequently, it is suggested you use the regular ChainRequest
    /// </summary>
    public class ChainRequestString : ChainRequestBase
    {
        public string search;
        public string endpoint;
    }

    /// <summary>
    /// Represents a single chain between a previous result field and a current search field
    /// </summary>
    public class Chaining
    {
        public string viewableIdentifier {get;set;}

        public int index {get;set;} = -1;
        public string getField {get;set;}
        public string searchField {get;set;}

        public override string ToString()
        {
            return viewableIdentifier ?? "";
        }
    }

    /// <summary>
    /// User-supplied config for relation listening in the chaining service
    /// </summary>
    public class RelationListenChainConfig : RelationListenConfig
    { 
        //public long lastId {get;set;} = -1;
        //public Dictionary<long, string> statuses {get;set;} = new Dictionary<long, string>();
        public List<string> chains {get;set;}
    }

    /// <summary>
    /// User-supplied config for listener... listening in the chaining service
    /// </summary>
    public class ListenerChainConfig
    {
        public Dictionary<long, Dictionary<long, string>> lastListeners {get;set;} = new Dictionary<long, Dictionary<long, string>>();
        public List<string> chains {get;set;}
    }

    /// <summary>
    /// The results from listening in the chaining service
    /// </summary>
    public class ListenResult
    {
        public Dictionary<long, Dictionary<long, string>> listeners {get;set;}
        public Dictionary<string, List<ExpandoObject>> chains {get;set;}
        public long lastId {get;set;}
        public List<string> warnings {get;set;} = new List<string>();
    }

    public class ChainServiceConfig
    {
        public int MaxChains {get;set;} = 5;
        public TimeSpan CompletionWaitUp {get;set;} = TimeSpan.FromMilliseconds(10);
        public string RequestRegex {get;set;} = @"^(?<endpoint>[a-z]+)(\.(?<chain>\d+[a-z]+(?:\$[a-z]+)?))*(~(?<name>[^\-]+))?(-(?<search>.+))?$";
        public string ChainRegex {get;set;} = @"^(?<index>\d+)(?<field>[a-z]+)(\$(?<searchfield>[a-z]+))?$";

        public JsonSerializerOptions JsonOptions {get;set;} = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// A service for chaining together separate requests. You can use the output of one request as the input 
    /// for future requests.
    /// </summary>
    public class ChainService
    {
        protected ChainServices services;
        protected RelationListenerService relationService;
        protected ILogger logger;
        protected ChainServiceConfig config;

        //These should all be... settings?

        public ChainService(ILogger<ChainService> logger, ChainServices services, RelationListenerService relationService, ChainServiceConfig config)
        {
            this.logger = logger;
            this.services = services;
            this.relationService = relationService;
            this.config = config;
        }

        public async Task SetupAsync() 
        { 
            //Come up with a better way to track all these setups, you keep forgetting some
            await services.content.SetupAsync(); 
            await services.watch.SetupAsync();
            await services.vote.SetupAsync();
        }

        //https://stackoverflow.com/a/26766221/1066474
        protected IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            if (!type.IsInterface)
                return type.GetProperties();

            return (new Type[] { type }).Concat(type.GetInterfaces()).SelectMany(i => i.GetProperties());
        }

        /// <summary>
        /// Using the given chaining, link the appropriate field(s) from old chains into the current search.
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="existingChains"></param>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        public List<long> LinkTosearch<S>(Chaining chain, List<List<IIdView>> existingChains, S search) where S : IIdSearcher
        {
            if(string.IsNullOrEmpty(chain.searchField))
                chain.searchField = "Ids";
            
            // Before going out and getting stuff, make sure ALL our fields are good. Reflection is expensive!
            if (chain.index < 0 || chain.getField == null || existingChains.Count <= chain.index)
                throw new BadRequestException($"Bad chain index or missing field: {chain}");

            var searchType = typeof(S);
            var searchProperties = GetProperties(searchType);
            var searchProperty = searchProperties.FirstOrDefault(x => x.Name.ToLower() == chain.searchField.ToLower());

            if (searchProperty == null)
                throw new BadRequestException($"Bad chain assign field: {chain}");

            if(searchProperty.PropertyType != typeof(List<long>))
                throw new BadRequestException($"Bad chain assign field type: {chain}");

            var ids = new List<long>();
            var analyze = existingChains[chain.index].FirstOrDefault();

            if(analyze != null)
            {
                var type = analyze.GetType();
                var properties = GetProperties(type);
                var property = properties.FirstOrDefault(x => x.Name.ToLower() == chain.getField.ToLower());

                Func<IIdView, IEnumerable<long>> selector = null;

                if (property != null)
                {
                    if (property.PropertyType == typeof(long))
                        selector = x => new[] { (long)property.GetValue(x) };
                    else if (property.PropertyType == typeof(List<long>)) //maybe need to get better about typing
                        selector = x => (List<long>)property.GetValue(x);
                }

                if (selector == null)
                    throw new BadRequestException($"Bad chain read field: {chain}");

                //Addrange so that we keep that old maxvalue
                ids.AddRange(existingChains[chain.index].SelectMany(selector));
            }
            // There's no "else" here because it is OK if there's no item to analyze: our search will simply 
            // be "empty". This does NOT mean "get all", it means "get none"

            //Ensure a true "empty search" if there were no results. There is a CHANCE that later linkings
            //will actually add values to this search field. This is OK; the max value will not break the search.
            if(ids.Count == 0)
                ids.Add(long.MaxValue);

            ((List<long>)searchProperty.GetValue(search)).AddRange(ids);

            return ids;
        }

        /// <summary>
        /// Parse a specially-formatted request into a temporary chain container. The container is not complete
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public ChainRequestString ParseInitialChainString(string request)
        {
            var match = Regex.Match(request, config.RequestRegex, RegexOptions.IgnoreCase);

            var result = new ChainRequestString()
            {
                search = match.Groups["search"].Value,
                endpoint = match.Groups["endpoint"].Value,
                name = match.Groups["name"].Value
            };

            //Convert chaining strings into proper chaining objects.
            result.chains = match.Groups["chain"].Captures.Select(x => 
            {
                var c = x.Value;
                var match = Regex.Match(c, config.ChainRegex, RegexOptions.IgnoreCase);

                var chaining = new Chaining()
                {
                    viewableIdentifier = c,
                    getField = match.Groups["field"].Value,
                    searchField = match.Groups["searchfield"].Value
                };

                int tempIndex = 0;

                if(!int.TryParse(match.Groups["index"].Value, out tempIndex))
                    throw new BadRequestException($"Can't parse chain index: {match.Groups["index"]}");

                chaining.index = tempIndex;
                return chaining;
            });

            if(string.IsNullOrWhiteSpace(result.search))
                result.search = "{}";
            
            return result;
        }

        /// <summary>
        /// The major "string request" based chaining. Repeatedly call this function for every chain request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="requester"></param>
        /// <param name="results"></param>
        /// <param name="previousResults"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        public Task ChainAsync(ChainRequestString data, Requester requester, List<List<IIdView>> previousResults)
        {
            if (data.endpoint == "file")
                return ChainStringAsync(data, services.file, requester, previousResults);
            else if (data.endpoint == "user")
                return ChainStringAsync(data, services.user, requester, previousResults);
            else if (data.endpoint == "content")
                return ChainStringAsync(data, services.content, requester, previousResults);
            else if (data.endpoint == "category")
                return ChainStringAsync(data, services.category, requester, previousResults);
            else if (data.endpoint == "comment")
                return ChainStringAsync(data, services.comment, requester, previousResults);
            else if (data.endpoint == "commentaggregate")
                return ChainStringAsync<CommentSearch, CommentAggregateView>(data, (s) => services.comment.SearchAggregateAsync(s, requester), previousResults);
            else if (data.endpoint == "activity")
                return ChainStringAsync(data, services.activity, requester, previousResults);
            else if (data.endpoint == "activityaggregate")
                return ChainStringAsync<ActivitySearch, ActivityAggregateView>(data, (s) => services.activity.SearchAggregateAsync(s, requester), previousResults);
            else if (data.endpoint == "watch")
                return ChainStringAsync(data, services.watch, requester, previousResults);
            else if (data.endpoint == "vote")
                return ChainStringAsync(data, services.vote, requester, previousResults);
            else
                throw new BadRequestException($"Unknown request: {data.endpoint}");
        }

        /// <summary>
        /// A special intermediate function for converting string chaining into real chaining. One extra step as shortcut:
        /// convert view service and requester to proper Func
        /// </summary>
        /// <param name="chainData"></param>
        /// <param name="service"></param>
        /// <param name="requester"></param>
        /// <param name="previousChains"></param>
        /// <typeparam name="S"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <returns></returns>
        protected Task ChainStringAsync<S,V>(ChainRequestString chainData, IViewReadService<V,S> service, Requester requester, List<List<IIdView>> previousChains)
            where S : IConstrainedSearcher where V : IIdView
        {
            return ChainStringAsync<S,V>(chainData, (s) => service.SearchAsync(s, requester), previousChains);
        }

        /// <summary>
        /// A special intermediate function for converting string chaining into real chaining.
        /// </summary>
        /// <param name="chainDataString"></param>
        /// <param name="search"></param>
        /// <param name="previousChains"></param>
        /// <typeparam name="S"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <returns></returns>
        protected Task ChainStringAsync<S,V>(ChainRequestString chainDataString, Func<S, Task<List<V>>> search, List<List<IIdView>> previousChains)
            where S : IConstrainedSearcher where V : IIdView
        {
            var chainData = new ChainRequest<S,V>(chainDataString) { retriever = search };

            try
            {
                chainData.baseSearch = JsonSerializer.Deserialize<S>(chainDataString.search, config.JsonOptions);
            }
            catch(Exception ex)
            {
                //I don't care too much about json deseralize errors, just the message. I don't even log it.
                throw new BadRequestException(ex.Message + " (CHAIN REMINDER: json search comes last AFTER all . chaining in a request)");
            }

            return ChainAsync(chainData, previousChains);
        }

        /// <summary>
        /// THE REAL PROPER CHAINING ENDPOINT! Will perform ONE chain request (after linking with previous chains)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="baggage"></param>
        /// <typeparam name="S"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <returns></returns>
        public async Task ChainAsync<S,V>(ChainRequest<S,V> data, List<List<IIdView>> previousChains)
            where V : IIdView where S : IConstrainedSearcher
        {
            Dictionary<string, PropertyInfo> properties = null;

            Type type = typeof(V);

            //My poor design has led to this...
            if(type == typeof(UserViewFull))
                type = typeof(UserViewBasic);

            var baseProperties = GetProperties(type);

            //Before doing ANYTHING, IMMEDIATELY convert fields to actual properties. It's easy if they pass us null: they want everything.
            if(data.fields == null)
            {
                properties = baseProperties.ToDictionary(x => x.Name, x => x);
            }
            else
            {
                var lowerFields = data.fields.Select(x => x.ToLower());
                properties = baseProperties.Where(x => lowerFields.Contains(x.Name.ToLower())).ToDictionary(x => x.Name, x => x);

                if(properties.Count != data.fields.Count)
                    throw new BadRequestException($"Unknown fields in list: {string.Join(",", data.fields)}");
            }

            //Parse the chains, get the ids. WARN: THIS IS DESTRUCTIVE TO DATA.BASESEARCH!!!
            foreach(var c in data.chains)
                LinkTosearch(c, previousChains, data.baseSearch);
            
            var myResults = await data.retriever(data.baseSearch);
            previousChains.Add(myResults.Cast<IIdView>().ToList());

            //Only add ones that aren't in the list
            foreach(var v in myResults)
            {
                if(!data.mergeList.Any(x => x.id == v.id))
                {
                    var result = new TaggedChainResult() { id = v.id, result = new ExpandoObject() };

                    foreach(var p in properties)
                        result.result.TryAdd(p.Key, p.Value.GetValue(v));

                    lock(data.mergeLock)
                    {
                        data.mergeList.Add(result);
                    }
                }
            }
        }

        //You don't want to call this repeatedly, so only call it on the request LISTS
        protected Dictionary<string, List<string>> FixFields(Dictionary<string, List<string>> fields)
        {
            if(fields == null)
                fields = new Dictionary<string, List<string>>();

            return fields.ToDictionary(x => x.Key, y => y.Value.SelectMany(z => z.Split(",", StringSplitOptions.RemoveEmptyEntries)).ToList());
        }

        protected Dictionary<string, List<ExpandoObject>> ChainResultToReturn(Dictionary<string, List<TaggedChainResult>> results)
        {
            return results.ToDictionary(x => x.Key, y => y.Value.Select(x => x.result).ToList());
        }

        protected void CheckChainLimit(int? count)//, double modifier = 1)
        {
            if(count != null && count > config.MaxChains)// * modifier)
                throw new BadRequestException($"Can't chain deeper than {config.MaxChains}");
        }

        /// <summary>
        /// Set up the given existing chain request from multi-chaining data (string version)
        /// </summary>
        /// <param name="chainBase"></param>
        protected ChainRequestString SetupChainRequestString(string request, Dictionary<string, List<TaggedChainResult>> results, Dictionary<string, List<string>> fields)
        {
            var data = ParseInitialChainString(request);
            data.mergeLock = results; //We can assume results won't change
            
            if(string.IsNullOrWhiteSpace(data.name))
                data.name = data.endpoint;

            //Yes, we lock ON the results themselves. It is spooky, but we assume the results object 
            //given won't change between calls
            lock (results)
            {
                if (!results.ContainsKey(data.name))
                    results.Add(data.name, new List<TaggedChainResult>());
            }

            data.mergeList = results[data.name];
            data.fields = fields.ContainsKey(data.name) ? fields[data.name] : null;

            return data;
        }

        /// <summary>
        /// A "full" run of chains using strings.
        /// </summary>
        /// <param name="requests"></param>
        /// <param name="fields"></param>
        /// <param name="requester"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, List<ExpandoObject>>> ChainAsync(List<string> requests, Dictionary<string, List<string>> fields, Requester requester)
        {
            var results = new Dictionary<string, List<TaggedChainResult>>();

            //Only do something if there's something to do
            if (requests != null && requests.Count > 0)
            {
                fields = FixFields(fields);
                CheckChainLimit(requests.Count);

                var previousChains = new List<List<IIdView>>();

                foreach (var request in requests)
                    await ChainAsync(SetupChainRequestString(request, results, fields), requester, previousChains);
            }

            return ChainResultToReturn(results);
        }

        protected class PhonyListenerList : IIdView
        {
            public long id {get;set;}
            public List<long> listeners {get;set;}
        }

        //protected class EntityRelationView : EntityRelation, IIdView
        //{
        //    public EntityRelationView(EntityRelation copy) : base(copy) {}
        //}

        //public Dictionary<long, Dictionary<long, string>> InstantListeners(IEnumerable<long> parentIds)
        //{
        //    var listeners = relationService.GetListenersInstant(parentIds);
        //}

        public async Task<ListenResult> ListenAsync(Dictionary<string, List<string>> fields, ListenerChainConfig listeners, RelationListenChainConfig actions, Requester requester, CancellationToken cancelToken)
        {
            var result = new ListenResult();

            //Since all this stuff happens at the SAME TIME on the SAME dbcontext (which usually doesn't happen), we need a lock to ensure only one is 
            //getting to it at a time.
            var semaphore = new SemaphoreSlim(1, 1);

            //Assume nothing changed in the result (it may just be a listener update), and better to send the same than to send nothing.
            if(actions != null)
                result.lastId = actions.lastId; 

            fields = FixFields(fields);

            var chainResults = new Dictionary<string, List<TaggedChainResult>>();
            List<Task> waiters = new List<Task>();

            CheckChainLimit(listeners?.chains?.Count);
            CheckChainLimit(actions?.chains?.Count);//,2);

            //A simple function-wide lock for asynchronous tasks. Should be safe... since it's all within the function.
            Func<Func<Task>, Task> lockAsync = async(a) =>
            {
                await semaphore.WaitAsync();
                try { await a(); }
                finally { semaphore.Release(); }
            };

            //A simple function to apply the given list of chains to the given list of views
            Func<List<string>, IEnumerable<IIdView>, Task> chainer = async (l, i) =>
            {
                if (l != null)
                {
                    //First result is just... the list of ID views. There are no other chain results
                    var tempViewResults = new List<List<IIdView>>() { i.ToList() };

                    //ONLY allow SINGLE access the database 
                    await lockAsync(async () =>
                    {
                        foreach (var chain in l)
                            await ChainAsync(SetupChainRequestString(chain, chainResults, fields), requester, tempViewResults);
                    });
                }
            };

            //Add a simple id as a signal on the chain result. Return the object representing the signal
            //(in case you want to add more to it)
            Func<string, long, ExpandoObject> addSignal = (key, id) =>
            {
                dynamic signal = new ExpandoObject();
                signal.id = id;

                if (!chainResults.ContainsKey(key))
                    chainResults.Add(key, new List<TaggedChainResult>());

                if (!chainResults[key].Any(x => x.id == signal.id))
                    chainResults[key].Add(new TaggedChainResult() { id = signal.id, result = signal });
                
                return signal;
            };
            

            //Create a new cancel source FROM the original token so that either us or the client can cancel
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
            {
                try
                {
                    if (actions != null)
                    {
                        //An ABSOLUTELY SILLY HACK because I don't actually know how to write this pattern using C#. I want an asynchronous... "task",
                        //and ugh maybe this IS how you do it? I mean that I want a section of code to run asynchronously with "await" and stuff,
                        //and get the "task" that represents this async code. Actually... maybe this IS how you do it.
                        Func<Task> run = async () =>
                        {
                            while (true) //I'm RELYING on the fact that OTHER tasks SHOULD have the proper long-polling timeout
                            {
                                var relations = await relationService.ListenAsync(actions, requester, linkedCts.Token);

                                //We can't use the results as-is. Some relations are actually SPECIAL; consider using services for this 
                                //later in case the meaning of the fields change!!
                                var baseViews = new List<BaseView>();
                                var clearContents = new List<long>();

                                foreach(var r in relations)
                                {
                                    BaseView v = null;

                                    //The real comment is something else: they want the real comment (it's just an edit so... they'll get the comment again)
                                    if(r.type.StartsWith(Keys.CommentHistoryHack) || r.type.StartsWith(Keys.CommentDeleteHack))
                                    {
                                        v = new BaseView() { id = -r.entityId1 };
                                    }
                                    else if (r.type == Keys.WatchUpdate)
                                    {
                                        ((dynamic)addSignal(Keys.ChainWatchUpdate, r.entityId1)).contentId = -r.entityId2;
                                    }
                                    else if (r.type == Keys.WatchDelete)
                                    {
                                        ((dynamic)addSignal(Keys.ChainWatchDelete, r.entityId1)).contentId = -r.entityId2;
                                    }
                                    //Oh just something probably normal I guess...
                                    else
                                    {
                                        v = new BaseView() { id = r.id };

                                        if(r.type == Keys.ActivityKey)
                                            clearContents.Add(-r.entityId2);
                                        else if(r.type == Keys.CommentHack)
                                            clearContents.Add(r.entityId1);
                                    }

                                    if(v != null)
                                        baseViews.Add(v);
                                }

                                //Inefficient, but I NEED to clear the notifications BEFORE chaining. This MIGHT be called WAY TOO OFTEN so...
                                //hopefully tracking the contents make it better
                                await lockAsync(() => services.watch.ClearAsyncFast(requester, actions.clearNotifications.Intersect(clearContents).ToArray()));
                                result.lastId = relations.Max(x => x.id);

                                await chainer(actions.chains, baseViews); //result.Select(x => new BaseView() {id = x.id}));
                                if (chainResults.Sum(x => x.Value.Count()) > 0)
                                    break;
                                else
                                    actions.lastId = result.lastId;
                                //the "else" makes it so we don't loop indefinitely getting the same instant completion 
                                //when nothing was chained, but is this OK logic?
                            }
                        }; 
                        waiters.Add(run());
                    }

                    //Only run the listeners that the user asked for
                    if (listeners != null)
                    {
                        if(listeners.lastListeners.Count > 0)
                        {
                            List<ContentView> allowedContent = null;
                            
                            //This also accesses the database, must only allow single access!
                            await lockAsync(async () => allowedContent = await services.content.SearchAsync(new ContentSearch() { Ids = listeners.lastListeners.Keys.ToList() }, requester));

                            foreach(var l in listeners.lastListeners.Keys.ToList())
                                if(l > 0 && !allowedContent.Any(x => x.id == l)) //This allows invalid range ids for fun debugging or feature stuff, but still gives privacy for private rooms
                                    listeners.lastListeners.Remove(l);
                        }

                        if(listeners.lastListeners.Count == 0)
                        {
                            result.warnings.Add("There were no valid contentIds in your listeners group; not listening");
                        }
                        else
                        {
                            //We wait a LITTLE BIT so that if comments don't complete, we will show up in the listener list.
                            await Task.Delay(5);

                            Func<Task> run = async () =>
                            {
                                result.listeners = await relationService.GetListenersAsync(listeners.lastListeners, requester, linkedCts.Token);
                                await chainer(listeners.chains, result.listeners.Select(x => new PhonyListenerList() { id = x.Key, listeners = x.Value.Keys.ToList() }));
                            };

                            waiters.Add(run());
                        }
                    }

                    if (waiters.Count == 0)
                        throw new BadRequestException("No listeners registered");

                    var completed = await Task.WhenAny(waiters.ToArray());

                    //This is funny: we want the completed task to throw its exception (wehnany doesn't do that)
                    await completed; //When done, this will just finish. When exceptioned, it will throw
                    await Task.Delay(config.CompletionWaitUp); //To allow some others to catch up if they're ALMOST done
                }
                finally
                {
                    linkedCts.Cancel();

                    try
                    {
                        //If people are still chaining, this should allow them to finish. But if they were still listening,
                        //they SHOULD'VE been cancelled and this will complete fast
                        await Task.WhenAll(waiters.ToArray());
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogDebug("One or more listener tasks cancelled on page completion (normal)");
                    }
                }
            }

            if (chainResults.Count > 0)
                result.chains = ChainResultToReturn(chainResults);

            return result;
        }
    }
}