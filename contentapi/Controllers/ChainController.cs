using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Views;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
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
                throw new InvalidOperationException($"Bad chain: {chain}");

            var analyze = existingChains[realIndex].FirstOrDefault();

            if(analyze == null)
            {
                logger.LogWarning("Linked chain had no elements, adding faulty value to simulate 0 search");
                return new List<long>() { long.MaxValue };
            }

            var type = analyze.GetType();
            var properties = type.GetProperties();
            var property = properties.FirstOrDefault(x => x.Name.ToLower() == field.ToLower());

            if (property == null || property.PropertyType != typeof(long))
                throw new InvalidOperationException($"Bad chain: {chain}");

            return existingChains[realIndex].Select(x => (long)property.GetValue(x)).ToList();
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

        protected async Task ChainAsync<S,V>(
            ChainData data, 
            IViewReadService<V,S> service, 
            Requester requester, 
            List<List<IIdView>> existingChains, 
            List<ChainResult> results,
            List<string> fields
        ) where V : IIdView where S : IIdSearcher
        {
            Dictionary<string, PropertyInfo> properties = null;

            Type type = typeof(V);

            if(type == typeof(UserViewFull))
                type = typeof(IUserViewBasic);

            //Before doing ANYTHING, IMMEDIATELY convert fields to actual properties. It's easy if they pass us null: they want everything.
            if(fields == null)
            {
                properties = type.GetProperties().ToDictionary(x => x.Name, x => x);
            }
            else
            {
                var lowerFields = fields.Select(x => x.ToLower());
                properties = type.GetProperties().Where(x => lowerFields.Contains(x.Name.ToLower())).ToDictionary(x => x.Name, x => x);

                if(properties.Count != fields.Count)
                    throw new InvalidOperationException($"Unknown fields in list: {string.Join(",", fields)}");
            }

            var searchobject = JsonSerializer.Deserialize<S>(data.search, jsonOptions);

            //Parse the chains, get the ids
            foreach(var c in data.chains)
                searchobject.Ids.AddRange(ParseChain(c, existingChains));

            var myResults = await service.SearchAsync(searchobject, requester);
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

        [HttpGet("chain")]
        public async Task<ActionResult<Dictionary<string, List<ExpandoObject>>>> ChainAsync([FromQuery]List<string> requests, [FromQuery]Dictionary<string, List<string>> fields)
        {
            logger.LogInformation($"ChainAsync called for {requests.Count} requests");

            if(requests == null)
                requests = new List<string>();
            if(fields == null)
                fields = new Dictionary<string, List<string>>();

            fields = fields.ToDictionary(x => x.Key, y => y.Value.SelectMany(z => z.Split(",", StringSplitOptions.RemoveEmptyEntries)).ToList());

            if(requests.Count > 5)
                throw new InvalidOperationException("Can't chain deeper than 5");

            var requester = GetRequesterNoFail();
            var results = new Dictionary<string, List<ChainResult>>();
            var userResults = new List<UserViewFull>();

            var chainResults = new List<List<IIdView>>();

            foreach(var request in requests)
            {
                var data = ParseChainData(request);

                if(!results.ContainsKey(data.endpoint))
                    results.Add(data.endpoint, new List<ChainResult>());
                
                var r = results[data.endpoint];
                var f = fields.ContainsKey(data.endpoint) ? fields[data.endpoint] : null;

                //Go find the endpoint
                if(data.endpoint == "file")
                    await ChainAsync(data, services.file, requester, chainResults, r, f);
                else if(data.endpoint == "user")
                    await ChainAsync(data, services.user, requester, chainResults, r, f);
                else if(data.endpoint == "content")
                    await ChainAsync(data, services.content, requester, chainResults, r, f);
                else if(data.endpoint == "category")
                    await ChainAsync(data, services.category, requester, chainResults, r, f);
                else if(data.endpoint == "comment")
                    await ChainAsync(data, services.comment, requester, chainResults, r, f);
                else if(data.endpoint == "activity")
                    await ChainAsync(data, services.activity, requester, chainResults, r, f);
            }

            return results.ToDictionary(x => x.Key, y => y.Value.Select(x => x.result).ToList());
        }

        [HttpGet("chain/docs")]
        public Task<ActionResult<string>> ChainDocsAsync()
        {
            return ThrowToAction(() => Task.FromResult(docService.GetString("read.chain", "en")));
        }
    }
}