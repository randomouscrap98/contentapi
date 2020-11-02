using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ContentSearch : BaseContentSearch
    {
        public string Keyword {get;set;}
        public string Type {get;set;}
        public List<string> NotTypes {get;set;} = new List<string>();
        public bool IncludeAbout {get;set;} = false;
    }

    public class ContentViewSourceProfile : Profile
    {
        public ContentViewSourceProfile()
        {
            //Can't do keyword, it's special search
            CreateMap<ContentSearch, EntitySearch>()
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.Type));
        }
    }

    public class ContentViewSource : BaseStandardViewSource<ContentView, EntityPackage, EntityGroup, ContentSearch>
    {
        public override string EntityType => Keys.ContentType;

        public ContentViewSource(ILogger<ContentViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) { }

        public override EntityPackage FromView(ContentView view)
        {
            var package = this.NewEntity(view.name, view.content);
            this.ApplyFromStandard(view, package, Keys.ContentType);

            foreach(var v in view.keywords)
            {
                package.Add(new EntityValue()
                {
                    entityId = view.id,
                    key = Keys.KeywordKey,
                    value = v,
                    createDate = null
                });
            }
            
            package.Entity.type += view.type;

            return package;
        }

        public override ContentView ToView(EntityPackage package)
        {
            var view = new ContentView();
            this.ApplyToStandard(package, view);

            view.name = package.Entity.name;
            view.content = package.Entity.content;
            view.type = package.Entity.type.Substring(Keys.ContentType.Length);

            foreach(var keyword in package.Values.Where(x => x.key == Keys.KeywordKey))
                view.keywords.Add(keyword.value);
            
            return view;
        }

        public override EntitySearch CreateSearch(ContentSearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += (search.Type ?? "%");
            return es;
        }

        public override async Task<IQueryable<EntityGroup>> ModifySearch(IQueryable<EntityGroup> query, ContentSearch search)
        {
            query = await base.ModifySearch(query, search);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
                query = await LimitByValue(query, Keys.KeywordKey, search.Keyword);
            
            return query;
        }

        public override async Task<IQueryable<long>> FinalizeQuery(IQueryable<EntityGroup> query, ContentSearch search)
        {
            if(search.NotTypes.Count > 0)
            {
                search.NotTypes = search.NotTypes.Select(x => EntityType + x).ToList();
                query = query.Where(x => !search.NotTypes.Contains(x.entity.type));
            }

            Dictionary<string, double> weights = new Dictionary<string, double>();

            double globalVoteWeight= 0;
            double globalWatchWeight = 0;

            //Just in case you want different weights
            if(search.Sort == "votes") {
                globalVoteWeight = 1;
            }
            else if (search.Sort == "watches") {
                globalWatchWeight = 1;
            }
            else if (search.Sort == "score") {
                globalVoteWeight = 1;
                globalWatchWeight = 1;
            }

            //ALL keys have to be present in the dictionary! We don't discriminate! And the SQL parameter 
            //list gets built with all keys regardless of if we use them!
            foreach (var voteWeight in Votes.VoteWeights)
                weights.Add(Keys.VoteRelation + voteWeight.Key, globalVoteWeight * voteWeight.Value);

            weights.Add(Keys.WatchRelation, globalWatchWeight);

            if(weights.Any(x => x.Value != 0))
            {
                logger.LogWarning("TEMP LOG: performing heavy content sorting query!");

                var condense = query.GroupBy(MainIdSelector).Select(x => x.Key);

                //The relation stuff!
                var joined = condense 
                    .GroupJoin((await Q<EntityRelation>()).Where(x => weights.Keys.Contains(x.type)), s => s, r => -r.entityId2, (s, r) => new { s = s, r = r })
                    .SelectMany(x => x.r.DefaultIfEmpty(), (x, y) => new { id = x.s, passthrough = y });

                var grouped = 
                    from j in joined
                    group j by j.id into g
                    select new { id = g.Key, sum = g.Sum(x =>
                        x.passthrough.type == Keys.WatchRelation ? weights[Keys.WatchRelation] : 
                        x.passthrough.type == Keys.VoteRelation + "b" ? weights[Keys.VoteRelation + "b"] :
                        x.passthrough.type == Keys.VoteRelation + "o" ? weights[Keys.VoteRelation + "o"] : 
                        x.passthrough.type == Keys.VoteRelation + "g" ? weights[Keys.VoteRelation + "g"] : 0)
                    };

                if(search.Reverse)
                    grouped = grouped.OrderByDescending(x => x.sum);
                else
                    grouped = grouped.OrderBy(x => x.sum);
                
                return grouped.Select(x => x.id);
            }

            return await base.FinalizeQuery(query, search);
        }
    }
}