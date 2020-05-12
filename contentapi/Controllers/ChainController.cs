using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Views;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ChainResult
    {
        public List<FileView> file {get;set;} = new List<FileView>();
        public List<UserViewBasic> user {get;set;} = new List<UserViewBasic>();
        public List<ContentView> content {get;set;} = new List<ContentView>();
        public List<CategoryView> category {get;set;} = new List<CategoryView>();
        public List<CommentView> comment {get;set;} = new List<CommentView>();
        public List<ActivityView> activity {get;set;} = new List<ActivityView>();
    }

    public class ReadController : BaseSimpleController
    {
        protected FileViewService file;
        protected UserViewService user;
        protected ContentViewService content;
        protected CategoryViewService category;
        protected CommentViewService comment;
        protected ActivityViewService activity;

        protected IMapper mapper;

        protected Regex requestRegex = new Regex(@"^(?<endpoint>[a-z]+)(\.(?<chain>\d+[a-z]+))*(-(?<search>.+))?$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected Regex chainRegex = new Regex(@"^(?<index>\d+)(?<field>[a-z]+)$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        protected JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        };

        public ReadController(ILogger<BaseSimpleController> logger, IMapper mapper,
            FileViewService file, 
            UserViewService user,
            ContentViewService content,
            CategoryViewService category,
            CommentViewService comment,
            ActivityViewService activity) : base(logger)
        {
            this.mapper = mapper;

            this.file = file;
            this.user = user;
            this.content = content;
            this.category = category;
            this.comment = comment;
            this.activity = activity;
        }

        public List<long> ParseChain(string chain, List<List<IdView>> existingChains)
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

        public async Task ChainAsync<S,V>(string search, IEnumerable<string> chains, IViewService<V,S> service, Requester requester, 
            List<List<IdView>> existingChains, List<V> results) where V : IdView where S : EntitySearchBase
        {
            if(string.IsNullOrWhiteSpace(search))
                search = "{}";

            var searchobject = JsonSerializer.Deserialize<S>(search, jsonOptions);

            //Parse the chains, get the ids
            foreach(var c in chains)
                searchobject.Ids.AddRange(ParseChain(c, existingChains));

            var myResults = await service.SearchAsync(searchobject, requester);
            existingChains.Add(myResults.Cast<IdView>().ToList());

            //Only add ones that aren't in the list
            foreach(var v in myResults)
            {
                if(!results.Any(x => x.id == v.id))
                    results.Add(v);
            }
        }

        [HttpGet("chain")]
        public async Task<ActionResult<ChainResult>> ChainAsync([FromQuery]List<string> requests)
        {
            logger.LogInformation($"ChainAsync called for {requests.Count} requests");

            if(requests == null)
                requests = new List<string>();

            if(requests.Count > 5)
                throw new InvalidOperationException("Can't chain deeper than 5");

            var requester = GetRequesterNoFail();
            var result = new ChainResult();
            var userResults = new List<UserViewFull>();

            var chainResults = new List<List<IdView>>();

            foreach(var request in requests)
            {
                var match = requestRegex.Match(request);

                var search = match.Groups["search"].Value;
                var endpoint = match.Groups["endpoint"].Value;
                var chains = match.Groups["chain"].Captures.Select(x => x.Value);

                //Go find the endpoint
                if(endpoint == "file")
                    await ChainAsync(search, chains, file, requester, chainResults, result.file);
                else if(endpoint == "user")
                    await ChainAsync(search, chains, user, requester, chainResults, userResults);
                else if(endpoint == "content")
                    await ChainAsync(search, chains, content, requester, chainResults, result.content);
                else if(endpoint == "category")
                    await ChainAsync(search, chains, category, requester, chainResults, result.category);
                else if(endpoint == "comment")
                    await ChainAsync(search, chains, comment, requester, chainResults, result.comment);
                else if(endpoint == "activity")
                    await ChainAsync(search, chains, activity, requester, chainResults, result.activity);
            }

            result.user = userResults.Select(x => mapper.Map<UserViewBasic>(x)).ToList();
            return result;
        }
    }
}