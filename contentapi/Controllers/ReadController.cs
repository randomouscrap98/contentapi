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
using contentapi.Services;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
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

    //public class ReadControllerProfile : Profile
    //{
    //    public ReadControllerProfile()
    //    {
    //        CreateMap<ReadController.RelationListenQuery, RelationListenConfig>().ReverseMap();
    //    }
    //}

    public class ReadController : BaseSimpleController
    {
        protected IMapper mapper;
        protected ILanguageService docService;
        protected ChainServices services;
        protected RelationListenerService relationService;

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

        public ReadController(ILogger<BaseSimpleController> logger, IMapper mapper, ILanguageService docService,
            ChainServices services, RelationListenerService relationService)
            : base(logger)
        {
            this.mapper = mapper;
            this.docService = docService;
            this.services = services;
            this.relationService = relationService;
        }

        protected override Task SetupAsync() { return services.content.SetupAsync(); }

        protected List<long> ChainIdSearch<S>(string chain, List<List<IIdView>> existingChains, S search) where S : IIdSearcher
        {
            var match = chainRegex.Match(chain);
            var index = match.Groups["index"].Value;
            var field = match.Groups["field"].Value;
            var searchfield = match.Groups["searchfield"].Value;

            if(string.IsNullOrEmpty(searchfield))
                searchfield = "Ids";
            
            //We "pre-add" some faulty value (which yes, increases processing for all requests)
            //to ensure that we're never searching "all" during a chain
            var ids = new List<long>() { long.MaxValue };

            //Parse the easy stuff before we get to reflection.
            int realIndex;

            if (index == null || field == null || !int.TryParse(index, out realIndex) || realIndex < 0 || existingChains.Count <= realIndex)
                throw new BadRequestException($"Bad chain index or missing field: {chain}");

            var analyze = existingChains[realIndex].FirstOrDefault();

            if(analyze != null)
            {
                var type = analyze.GetType();
                var properties = GetProperties(type);
                var property = properties.FirstOrDefault(x => x.Name.ToLower() == field.ToLower());

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
                ids.AddRange(existingChains[realIndex].SelectMany(selector));
            }

            //NOW, let's see about that field we'll be assigning to
            var searchType = typeof(S);
            var searchProperties = GetProperties(searchType);
            var searchProperty = searchProperties.FirstOrDefault(x => x.Name.ToLower() == searchfield.ToLower());

            if (searchProperty == null)
                throw new BadRequestException($"Bad chain assign field: {chain}");

            if(searchProperty.PropertyType != typeof(List<long>))
                throw new BadRequestException($"Bad chain assign field type: {chain}");
            
            ((List<long>)searchProperty.GetValue(search)).AddRange(ids);
            return ids;
        }

        public class ChainData
        {
            public string search;
            public string endpoint;
            public IEnumerable<string> chains;
        }

        public class ChainResult
        {
            public ExpandoObject result;
            public long id;
        }

        protected ChainData ParseChainData(string request)
        {
            var match = requestRegex.Match(request);

            var result = new ChainData()
            {
                search = match.Groups["search"].Value,
                endpoint = match.Groups["endpoint"].Value,
                chains = match.Groups["chain"].Captures.Select(x => x.Value)
            };

            if(string.IsNullOrWhiteSpace(result.search))
                result.search = "{}";
            
            return result;
        }

        protected Task ChainAsync<S,V>(
            ChainData data, 
            IViewReadService<V,S> service, 
            Requester requester, 
            List<List<IIdView>> existingChains, 
            List<ChainResult> results,
            List<string> fields
        ) where V : IIdView where S : IConstrainedSearcher
        {
            return ChainAsync<S,V>(data, (s) => service.SearchAsync(s, requester), existingChains, results, fields);
        }

        //Eventually move this to something else
        //https://stackoverflow.com/a/26766221/1066474
        protected IEnumerable<PropertyInfo> GetProperties(Type type)
        {
            if (!type.IsInterface)
                return type.GetProperties();

            return (new Type[] { type }).Concat(type.GetInterfaces()).SelectMany(i => i.GetProperties());
        }

        protected async Task ChainAsync<S,V>(
            ChainData data, 
            Func<S, Task<List<V>>> search, 
            List<List<IIdView>> existingChains, 
            List<ChainResult> results,
            List<string> fields
        ) where V : IIdView where S : IConstrainedSearcher
        {
            Dictionary<string, PropertyInfo> properties = null;

            Type type = typeof(V);

            //My poor design has led to this...
            if(type == typeof(UserViewFull))
                type = typeof(UserViewBasic);

            var baseProperties = GetProperties(type);

            //Before doing ANYTHING, IMMEDIATELY convert fields to actual properties. It's easy if they pass us null: they want everything.
            if(fields == null)
            {
                properties = baseProperties.ToDictionary(x => x.Name, x => x);
            }
            else
            {
                var lowerFields = fields.Select(x => x.ToLower());
                properties = baseProperties.Where(x => lowerFields.Contains(x.Name.ToLower())).ToDictionary(x => x.Name, x => x);

                if(properties.Count != fields.Count)
                    throw new BadRequestException($"Unknown fields in list: {string.Join(",", fields)}");
            }

            S searchobject;

            try
            {
                searchobject = JsonSerializer.Deserialize<S>(data.search, jsonOptions);
            }
            catch(Exception ex)
            {
                //I don't care too much about json deseralize errors, just the message. I don't even log it.
                throw new BadRequestException(ex.Message + " (CHAIN REMINDER: json search comes last AFTER all . chaining in a request)");
            }

            //Parse the chains, get the ids
            foreach(var c in data.chains)
                ChainIdSearch(c, existingChains, searchobject);
            
            var myResults = await search(searchobject);
            existingChains.Add(myResults.Cast<IIdView>().ToList());

            //Only add ones that aren't in the list
            foreach(var v in myResults)
            {
                if(!results.Any(x => x.id == v.id))
                {
                    var result = new ChainResult() { id = v.id, result = new ExpandoObject() };

                    foreach(var p in properties)
                        result.result.TryAdd(p.Key, p.Value.GetValue(v));

                    //We HOPE that the results won't change objects... this is very bad but
                    //ugh whatever I don't have time to go around making lock objects
                    lock(results)
                    {
                        results.Add(result);
                    }
                }
            }
        }

        protected async Task ChainAsync(
            string request,
            Requester requester,
            Dictionary<string, List<ChainResult>> results, 
            List<List<IIdView>> chainResults,
            Dictionary<string, List<string>> fields)
        {
            var data = ParseChainData(request);

            //Again, BE CAREFUL with locking on objects like this! I know the results object most likely won't
            //get reassigned but you never know!!
            lock(results)
            {
                if (!results.ContainsKey(data.endpoint))
                    results.Add(data.endpoint, new List<ChainResult>());
            }

            var r = results[data.endpoint];
            var f = fields.ContainsKey(data.endpoint) ? fields[data.endpoint] : null;

            //Go find the endpoint
            if (data.endpoint == "file")
                await ChainAsync(data, services.file, requester, chainResults, r, f);
            else if (data.endpoint == "user")
                await ChainAsync(data, services.user, requester, chainResults, r, f);
            else if (data.endpoint == "content")
                await ChainAsync(data, services.content, requester, chainResults, r, f);
            else if (data.endpoint == "category")
                await ChainAsync(data, services.category, requester, chainResults, r, f);
            else if (data.endpoint == "comment")
                await ChainAsync(data, services.comment, requester, chainResults, r, f);
            else if (data.endpoint == "commentaggregate")
                await ChainAsync<CommentSearch, CommentAggregateView>(data, (s) => services.comment.SearchAggregateAsync(s, requester), chainResults, r, f);
            else if (data.endpoint == "activity")
                await ChainAsync(data, services.activity, requester, chainResults, r, f);
            else if (data.endpoint == "activityaggregate")
                await ChainAsync<ActivitySearch, ActivityAggregateView>(data, (s) => services.activity.SearchAggregateAsync(s, requester), chainResults, r, f);
            else if (data.endpoint == "watch")
                await ChainAsync(data, services.watch, requester, chainResults, r, f);
            else if (data.endpoint == "vote")
                await ChainAsync(data, services.vote, requester, chainResults, r, f);
        }

        protected Dictionary<string, List<string>> FixFields(Dictionary<string, List<string>> fields)
        {
            if(fields == null)
                fields = new Dictionary<string, List<string>>();

            return fields.ToDictionary(x => x.Key, y => y.Value.SelectMany(z => z.Split(",", StringSplitOptions.RemoveEmptyEntries)).ToList());
        }

        protected Dictionary<string, List<ExpandoObject>> ChainResultToReturn(Dictionary<string, List<ChainResult>> results)
        {
            return results.ToDictionary(x => x.Key, y => y.Value.Select(x => x.result).ToList());
        }

        protected void CheckChainLimit(int? count)
        {
            if(count != null && count > 5)
                throw new BadRequestException("Can't chain deeper than 5");
        }

        [HttpGet("chain")]
        public Task<ActionResult<Dictionary<string, List<ExpandoObject>>>> ChainAsync([FromQuery]List<string> requests, [FromQuery]Dictionary<string, List<string>> fields)
        {
            return ThrowToAction(async() =>
            {
                if (requests == null)
                    requests = new List<string>();

                fields = FixFields(fields);

                CheckChainLimit(requests.Count);

                var requester = GetRequesterNoFail();
                var results = new Dictionary<string, List<ChainResult>>();
                var userResults = new List<UserViewFull>();

                var chainResults = new List<List<IIdView>>();

                foreach (var request in requests)
                {
                    await ChainAsync(request, requester, results, chainResults, fields);
                }

                return ChainResultToReturn(results);
            });
        }

        [HttpGet("chain/docs")]
        public Task<ActionResult<string>> ChainDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("read.chain", "en")));
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


        [HttpGet("listen")]
        [Authorize]
        public Task<ActionResult<ListenResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, [FromQuery]string listeners, [FromQuery]string actions, CancellationToken cancelToken)
        {
            return ThrowToAction(async () =>
            {
                var result = new ListenResult();
                var requester = GetRequesterNoFail();
                fields = FixFields(fields);

                var chainResults = new Dictionary<string, List<ChainResult>>();

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
                                while(true) //I'm RELYING on the fact that OTHER tasks SHOULD have the proper long-polling timeout
                                {
                                    var result = relationService.ListenAsync(rConfig, requester, linkedCts.Token).Result;
                                    chainer(actionObject.chain, result.Select(x => new EntityRelationView(x))).Wait();
                                    if(chainResults.Sum(x => x.Value.Count()) > 0)
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

                if(chainResults.Count > 0)
                    result.chain = ChainResultToReturn(chainResults);

                return result;
            });
        }

        [HttpGet("listen/docs")]
        public Task<ActionResult<string>> ListenDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("read.listen", "en")));
        }
    }
}