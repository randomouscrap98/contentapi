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
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
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

    public class ChainRequest<S, V> where V : IIdView where S : IIdSearcher
    {
        public S baseSearch {get;set;}
        public Func<S, Task<List<V>>> retriever {get;set;}
        public IEnumerable<Chaining> chains {get;set;}
    }

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

    //Some of this stuff MIGHT go back in
    public class ChainBaggage
    {
        public List<List<IIdView>> previousChains {get;set;}
        public List<TaggedChainResult> mergeList {get;set;}
        public List<string> fields {get;set;}
    }

    public class TaggedChainResult
    {
        public ExpandoObject result {get;set;}
        public long id {get;set;}
    }


    public class ChainService
    {
        protected IMapper mapper;
        protected ILanguageService docService;
        protected ChainServices services;
        protected RelationListenerService relationService;
        protected ILogger logger;

        //These should all be... settings?
        protected Regex requestRegex = new Regex(@"^(?<endpoint>[a-z]+)(\.(?<chain>\d+[a-z]+(?:\$[a-z]+)?))*(-(?<search>.+))?$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected Regex chainRegex = new Regex(@"^(?<index>\d+)(?<field>[a-z]+)(\$(?<searchfield>[a-z]+))?$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected TimeSpan completionWaitUp = TimeSpan.FromMilliseconds(10);
        protected JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        public ChainService(ILogger<ChainService> logger, IMapper mapper, ILanguageService docService,
            ChainServices services, RelationListenerService relationService)
        {
            this.logger = logger;
            this.mapper = mapper;
            this.docService = docService;
            this.services = services;
            this.relationService = relationService;
        }

        public Task SetupAsync() { return services.content.SetupAsync(); }

        //Eventually move this to something else
        //https://stackoverflow.com/a/26766221/1066474
        protected IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            if (!type.IsInterface)
                return type.GetProperties();

            return (new Type[] { type }).Concat(type.GetInterfaces()).SelectMany(i => i.GetProperties());
        }

        protected List<long> ChainIdSearch<S>(Chaining chain, List<List<IIdView>> existingChains, S search) where S : IIdSearcher
        {
            if(string.IsNullOrEmpty(chain.searchField))
                chain.searchField = "Ids";
            
            //We "pre-add" some faulty value (which yes, increases processing for all requests)
            //to ensure that we're never searching "all" during a chain
            var ids = new List<long>() { long.MaxValue };

            if (chain.index < 0 || chain.getField == null || existingChains.Count <= chain.index)
                throw new BadRequestException($"Bad chain index or missing field: {chain}");

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

            //NOW, let's see about that field we'll be assigning to
            var searchType = typeof(S);
            var searchProperties = GetProperties(searchType);
            var searchProperty = searchProperties.FirstOrDefault(x => x.Name.ToLower() == chain.searchField.ToLower());

            if (searchProperty == null)
                throw new BadRequestException($"Bad chain assign field: {chain}");

            if(searchProperty.PropertyType != typeof(List<long>))
                throw new BadRequestException($"Bad chain assign field type: {chain}");
            
            ((List<long>)searchProperty.GetValue(search)).AddRange(ids);
            return ids;
        }

        protected class ChainDataRaw
        {
            public string search;
            public string endpoint;
            public IEnumerable<string> chains;
        }

        protected ChainDataRaw ParseChainDataRaw(string request)
        {
            var match = requestRegex.Match(request);

            var result = new ChainDataRaw()
            {
                search = match.Groups["search"].Value,
                endpoint = match.Groups["endpoint"].Value,
                chains = match.Groups["chain"].Captures.Select(x => x.Value)
            };

            if(string.IsNullOrWhiteSpace(result.search))
                result.search = "{}";
            
            return result;
        }

        //The major "string request" based chaining endpoint
        public Task ChainAsync(string request, Requester requester, 
            Dictionary<string, List<TaggedChainResult>> results, 
            List<List<IIdView>> chainResults,
            Dictionary<string, List<string>> fields)
        {
            var data = ParseChainDataRaw(request);

            lock(results)
            {
                if (!results.ContainsKey(data.endpoint))
                    results.Add(data.endpoint, new List<TaggedChainResult>());
            }

            var baggage = new ChainBaggage()
            {
                previousChains = chainResults,
                mergeList = results[data.endpoint],
                fields = fields.ContainsKey(data.endpoint) ? fields[data.endpoint] : null
            };

            if (data.endpoint == "file")
                return ChainRawAsync(data, services.file, requester, baggage);
            else if (data.endpoint == "user")
                return ChainRawAsync(data, services.user, requester, baggage);
            else if (data.endpoint == "content")
                return ChainRawAsync(data, services.content, requester, baggage);
            else if (data.endpoint == "category")
                return ChainRawAsync(data, services.category, requester, baggage);
            else if (data.endpoint == "comment")
                return ChainRawAsync(data, services.comment, requester, baggage);
            else if (data.endpoint == "commentaggregate")
                return ChainRawAsync<CommentSearch, CommentAggregateView>(data, (s) => services.comment.SearchAggregateAsync(s, requester), baggage);
            else if (data.endpoint == "activity")
                return ChainRawAsync(data, services.activity, requester, baggage);
            else if (data.endpoint == "activityaggregate")
                return ChainRawAsync<ActivitySearch, ActivityAggregateView>(data, (s) => services.activity.SearchAggregateAsync(s, requester), baggage);
            else if (data.endpoint == "watch")
                return ChainRawAsync(data, services.watch, requester, baggage);
            else if (data.endpoint == "vote")
                return ChainRawAsync(data, services.vote, requester, baggage);
            else
                throw new BadRequestException($"Unknown request: {data.endpoint}");
        }

        //"Raw" chaining is a kind of staging area before going to the real, properly set up chaining.
        protected Task ChainRawAsync<S,V>(ChainDataRaw chainData, IViewReadService<V,S> service, Requester requester, ChainBaggage baggage) 
            where S : IConstrainedSearcher where V : IIdView
        {
            return ChainRawAsync<S,V>(chainData, (s) => service.SearchAsync(s, requester), baggage);
        }

        protected Task ChainRawAsync<S,V>(ChainDataRaw chainDataRaw, Func<S, Task<List<V>>> search, ChainBaggage baggage) 
            where S : IConstrainedSearcher where V : IIdView
        {
            var chainData = new ChainRequest<S,V>() { retriever = search };

            try
            {
                chainData.baseSearch = JsonSerializer.Deserialize<S>(chainDataRaw.search, jsonOptions);
            }
            catch(Exception ex)
            {
                //I don't care too much about json deseralize errors, just the message. I don't even log it.
                throw new BadRequestException(ex.Message + " (CHAIN REMINDER: json search comes last AFTER all . chaining in a request)");
            }

            chainData.chains = chainDataRaw.chains.Select(c => 
            {
                var match = chainRegex.Match(c);

                var chaining = new Chaining()
                {
                    getField = match.Groups["field"].Value,
                    searchField = match.Groups["searchfield"].Value
                };

                int tempIndex = 0;

                if(!int.TryParse(match.Groups["index"].Value, out tempIndex))
                    throw new BadRequestException($"Can't parse chain index: {match.Groups["index"]}");

                chaining.index = tempIndex;
                return chaining;
            });

            return ChainAsync(chainData, baggage);
        }

        /// <summary>
        /// THE REAL PROPER CHAINING ENDPOINT! Will perform ONE chain request (after linking with previous chains)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="baggage"></param>
        /// <typeparam name="S"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <returns></returns>
        public async Task ChainAsync<S,V>(ChainRequest<S,V> data, ChainBaggage baggage) 
            where V : IIdView where S : IConstrainedSearcher
        {
            Dictionary<string, PropertyInfo> properties = null;

            Type type = typeof(V);

            //My poor design has led to this...
            if(type == typeof(UserViewFull))
                type = typeof(UserViewBasic);

            var baseProperties = GetProperties(type);

            //Before doing ANYTHING, IMMEDIATELY convert fields to actual properties. It's easy if they pass us null: they want everything.
            if(baggage.fields == null)
            {
                properties = baseProperties.ToDictionary(x => x.Name, x => x);
            }
            else
            {
                var lowerFields = baggage.fields.Select(x => x.ToLower());
                properties = baseProperties.Where(x => lowerFields.Contains(x.Name.ToLower())).ToDictionary(x => x.Name, x => x);

                if(properties.Count != baggage.fields.Count)
                    throw new BadRequestException($"Unknown fields in list: {string.Join(",", baggage.fields)}");
            }

            //Parse the chains, get the ids. WARN: THIS IS DESTRUCTIVE TO DATA.BASESEARCH!!!
            foreach(var c in data.chains)
                ChainIdSearch(c, baggage.previousChains, data.baseSearch);
            
            var myResults = await data.retriever(data.baseSearch);
            baggage.previousChains.Add(myResults.Cast<IIdView>().ToList());

            //Only add ones that aren't in the list
            foreach(var v in myResults)
            {
                if(!baggage.mergeList.Any(x => x.id == v.id))
                {
                    var result = new TaggedChainResult() { id = v.id, result = new ExpandoObject() };

                    foreach(var p in properties)
                        result.result.TryAdd(p.Key, p.Value.GetValue(v));

                    //We HOPE that the results won't change objects... this is very bad but
                    //ugh whatever I don't have time to go around making lock objects
                    lock(baggage.mergeList)
                    {
                        baggage.mergeList.Add(result);
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

        protected void CheckChainLimit(int? count)
        {
            if(count != null && count > 5)
                throw new BadRequestException("Can't chain deeper than 5");
        }

        public async Task<Dictionary<string, List<ExpandoObject>>> ChainAsync(List<string> requests, Dictionary<string, List<string>> fields, Requester requester)
        {
            if (requests == null)
                requests = new List<string>();

            fields = FixFields(fields);
            CheckChainLimit(requests.Count);

            var results = new Dictionary<string, List<TaggedChainResult>>();
            var chainResults = new List<List<IIdView>>();

            foreach (var request in requests)
                await ChainAsync(request, requester, results, chainResults, fields);

            return ChainResultToReturn(results);
        }

        public class RelationListenQuery 
        { 
            public long lastId {get;set;} = -1;
            public Dictionary<string, string> statuses {get;set;} = new Dictionary<string, string>();
            public List<string> chain {get;set;}
        }

        public class ListenerQuery
        {
            public Dictionary<string, Dictionary<string, string>> lastListeners {get;set;} = new Dictionary<string, Dictionary<string, string>>();
            public List<string> chain {get;set;}
        }

        public class ListenResult
        {
            public Dictionary<string, Dictionary<string, string>> listeners {get;set;}
            public Dictionary<string, List<ExpandoObject>> chain {get;set;}
        }

        public class PhonyListenerList : IIdView
        {
            public long id {get;set;}
            public List<long> listeners {get;set;}
        }

        public class EntityRelationView : EntityRelation, IIdView
        {
            public EntityRelationView(EntityRelation copy) : base(copy) {}
        }

        public async Task<ListenResult> ListenAsync(Dictionary<string, List<string>> fields, string listeners, string actions, Requester requester, CancellationToken cancelToken)
        {
            var result = new ListenResult();
            fields = FixFields(fields);

            var chainResults = new Dictionary<string, List<TaggedChainResult>>();

            List<Task> waiters = new List<Task>();

            var listenerObject = JsonSerializer.Deserialize<ListenerQuery>(listeners ?? "null", jsonOptions);
            var actionObject = JsonSerializer.Deserialize<RelationListenQuery>(actions ?? "null", jsonOptions);

            CheckChainLimit(listenerObject?.chain?.Count);
            CheckChainLimit(actionObject?.chain?.Count);

            Func<List<string>, IEnumerable<IIdView>, Task> chainer = async (l, i) =>
            {
                if (l != null)
                {
                        //First result is the comments we got
                        var tempViewResults = new List<List<IIdView>>() { i.ToList() };

                    foreach (var chain in l)
                        await ChainAsync(chain, requester, chainResults, tempViewResults, fields);
                }
            };

            //Create a new cancel source FROM the original token so that either us or the client can cancel
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
            {
                try
                {
                    if (actionObject != null)
                    {
                        var rConfig = new RelationListenConfig() { lastId = actionObject.lastId };
                        rConfig.statuses = actionObject.statuses.ToDictionary(x => long.Parse(x.Key), y => y.Value);

                        waiters.Add(Task.Run(() =>
                        {
                            while (true) //I'm RELYING on the fact that OTHER tasks SHOULD have the proper long-polling timeout
                                {
                                var result = relationService.ListenAsync(rConfig, requester, linkedCts.Token).Result;
                                chainer(actionObject.chain, result.Select(x => new EntityRelationView(x))).Wait();
                                if (chainResults.Sum(x => x.Value.Count()) > 0)
                                    break;
                            }
                        }, linkedCts.Token));
                    }

                    //Only run the listeners that the user asked for
                    if (listenerObject != null)
                    {
                        //We wait a LITTLE BIT so that if comments don't complete, we will show up in the listener list.
                        await Task.Delay(5);
                        waiters.Add(
                            relationService.GetListenersAsync(listenerObject.lastListeners.ToDictionary(
                                    x => long.Parse(x.Key),
                                    y => y.Value.ToDictionary(k => long.Parse(k.Key), v => v.Value)),
                                requester, linkedCts.Token)
                            .ContinueWith(t =>
                            {
                                result.listeners = t.Result.ToDictionary(x => x.Key.ToString(), x => x.Value.ToDictionary(k => k.ToString(), v => v.Value));
                                return chainer(listenerObject.chain, t.Result.Select(x => new PhonyListenerList() { id = x.Key, listeners = x.Value.Keys.ToList() }));
                            })
                        );
                    }

                    if (waiters.Count == 0)
                        throw new BadRequestException("No listeners registered");

                    await Task.WhenAny(waiters.ToArray());
                    await Task.Delay(completionWaitUp); //To allow some others to catch up if they're ALMOST done
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
                result.chain = ChainResultToReturn(chainResults);

            return result;
        }
    }
}