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
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    }

    public class ReadController : BaseSimpleController
    {
        protected IMapper mapper;
        protected ILanguageService docService;
        protected ChainServices services;

        protected Regex requestRegex = new Regex(@"^(?<endpoint>[a-z]+)(\.(?<chain>\d+[a-z]+))*(-(?<search>.+))?$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected Regex chainRegex = new Regex(@"^(?<index>\d+)(?<field>[a-z]+)$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        public ReadController(ILogger<BaseSimpleController> logger, IMapper mapper, ILanguageService docService,
            ChainServices services) : base(logger)
        {
            this.mapper = mapper;
            this.docService = docService;
            this.services = services;
        }

        protected List<long> ParseChain(string chain, List<List<IIdView>> existingChains)
        {
            var match = chainRegex.Match(chain);
            var index = match.Groups["index"].Value;
            var field = match.Groups["field"].Value;
            int realIndex;

            if (index == null || field == null || !int.TryParse(index, out realIndex) || realIndex < 0 || existingChains.Count <= realIndex)
                throw new BadRequestException($"Bad chain: {chain}");

            var analyze = existingChains[realIndex].FirstOrDefault();

            if(analyze == null)
            {
                logger.LogWarning("Linked chain had no elements, adding faulty value to simulate 0 search");
                return new List<long>() { long.MaxValue };
            }

            var type = analyze.GetType();
            var properties = type.GetProperties();
            var property = properties.FirstOrDefault(x => x.Name.ToLower() == field.ToLower());

            Func<IIdView, IEnumerable<long>> selector = null;

            if (property != null)
            {
                if(property.PropertyType == typeof(long))
                    selector = x => new [] { (long)property.GetValue(x) };
                else if(property.PropertyType == typeof(List<long>)) //maybe need to get better about typing
                    selector = x => (List<long>)property.GetValue(x);
            } 

            if(selector == null)
                throw new BadRequestException($"Bad chain: {chain}");

            return existingChains[realIndex].SelectMany(selector).ToList();
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

            if(type == typeof(UserViewFull))
                type = typeof(IUserViewBasic);

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

            var searchobject = JsonSerializer.Deserialize<S>(data.search, jsonOptions);

            //Parse the chains, get the ids
            foreach(var c in data.chains)
                searchobject.Ids.AddRange(ParseChain(c, existingChains));
            
            //Oops, we're searching for NOTHING... yeah, this bad design is STILL BITING ME AAAGHH
            if(searchobject.Ids.Count == 0)
                return;

            var myResults = await search(searchobject); //service.SearchAsync(searchobject, requester);
            existingChains.Add(myResults.Cast<IIdView>().ToList());

            //Only add ones that aren't in the list
            foreach(var v in myResults)
            {
                if(!results.Any(x => x.id == v.id))
                {
                    var result = new ChainResult() { id = v.id, result = new ExpandoObject() };

                    foreach(var p in properties)
                        result.result.TryAdd(p.Key, p.Value.GetValue(v));

                    results.Add(result);
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

            if (!results.ContainsKey(data.endpoint))
                results.Add(data.endpoint, new List<ChainResult>());

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
            else if (data.endpoint == "watch")
                await ChainAsync(data, services.watch, requester, chainResults, r, f);
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
        public async Task<ActionResult<Dictionary<string, List<ExpandoObject>>>> ChainAsync([FromQuery]List<string> requests, [FromQuery]Dictionary<string, List<string>> fields)
        {
            if(requests == null)
                requests = new List<string>();

            fields = FixFields(fields);

            CheckChainLimit(requests.Count);

            var requester = GetRequesterNoFail();
            var results = new Dictionary<string, List<ChainResult>>();
            var userResults = new List<UserViewFull>();

            var chainResults = new List<List<IIdView>>();

            foreach(var request in requests)
            {
                await ChainAsync(request, requester, results, chainResults, fields);
            }

            return ChainResultToReturn(results);
        }

        [HttpGet("chain/docs")]
        public Task<ActionResult<string>> ChainDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("read.chain", "en")));
        }

        public class CommentListenQuery : CommentListenConfig 
        { 
            public List<string> chain {get;set;} //= new List<string>();
        }

        public class ListenerQuery
        {
            public Dictionary<string, List<long>> parentIdsLast {get;set;}
            public List<string> chain {get;set;} //= new List<string>();
        }

        public class ListenResult
        {
            public List<CommentView> comments {get;set;}
            public Dictionary<string, List<long>> listeners {get;set;}
            public Dictionary<string, List<ExpandoObject>> chain {get;set;}
        }

        public class PhonyListenerList : IIdView
        {
            public long id {get;set;}
            public List<long> listeners {get;set;}
        }

        //public void MergeChainResults(Dictionary<string, List<ExpandoObject>> baseChain, Dictionary<string, List<ExpandoObject>> join)
        //{
        //    foreach(var chain in join)
        //    {
        //        if(!baseChain.ContainsKey(chain.Key))
        //            baseChain.Add(chain.Key, new List<ExpandoObject>());
        //        

        //    }
        //}

        [HttpGet("listen")]
        [Authorize]
        public Task<ActionResult<ListenResult>> ListenAsync([FromQuery]Dictionary<string, List<string>> fields, [FromQuery]string comment, [FromQuery]string listener, CancellationToken cancelToken)
        {
            return ThrowToAction(async () =>
            {
                var result = new ListenResult();
                var requester = GetRequesterNoFail();
                fields = FixFields(fields);

                var chainResults = new Dictionary<string, List<ChainResult>>();

                //All the 
                Task<Dictionary<long, List<CommentListener>>> listenWait = null;
                Task<List<CommentView>> commentWait = null;
                List<Task> waiters = new List<Task>();

                var listenerObject = JsonSerializer.Deserialize<ListenerQuery>(listener ?? "{}", jsonOptions);
                var commentObject = JsonSerializer.Deserialize<CommentListenQuery>(comment ?? "{}", jsonOptions);

                CheckChainLimit(listenerObject?.chain?.Count);
                CheckChainLimit(commentObject?.chain?.Count);

                //Create a new cancel source FROM the original token so that either us or the client can cancel
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
                {
                    try
                    {
                        //Only run the listeners that the user asked for
                        if (listenerObject?.parentIdsLast != null)
                        {
                            listenWait = services.comment.GetListenersAsync(listenerObject.parentIdsLast.ToDictionary(x => long.Parse(x.Key), y => y.Value), requester, linkedCts.Token);
                            waiters.Add(listenWait);
                        }

                        if (commentObject?.parentIds != null)
                        {
                            commentWait = services.comment.ListenAsync(commentObject, requester, linkedCts.Token);
                            waiters.Add(commentWait);
                        }

                        if (waiters.Count == 0)
                            throw new BadRequestException("No listeners registered");

                        await Task.WhenAny(waiters.ToArray());

                        //Fill in data based on who is finished
                        if (commentWait != null && commentWait.IsCompleted)
                        {
                            result.comments = commentWait.Result;

                            if(commentObject.chain != null)
                            {
                                //First result is the comments we got
                                var tempViewResults = new List<List<IIdView>>() { result.comments.Cast<IIdView>().ToList() };

                                foreach(var chain in commentObject.chain)
                                    await ChainAsync(chain, requester, chainResults, tempViewResults, fields);
                            }
                        }
                        if (listenWait != null && listenWait.IsCompleted)
                        {
                            result.listeners = listenWait.Result.ToDictionary(x => x.Key.ToString(), x => x.Value.Select(x => x.UserId).ToList());

                            if(listenerObject.chain != null)
                            {
                                //First result is the comments we got
                                var tempViewResults = new List<List<IIdView>>() { 
                                    result.listeners.Select(x => new PhonyListenerList() {id = long.Parse(x.Key), listeners = x.Value })
                                .Cast<IIdView>().ToList() };

                                foreach(var chain in listenerObject.chain)
                                    await ChainAsync(chain, requester, chainResults, tempViewResults, fields);
                            }
                        }
                    }
                    finally
                    {
                        linkedCts.Cancel();

                        try
                        {
                            await Task.WhenAll(waiters.ToArray());
                        }
                        catch(OperationCanceledException)
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