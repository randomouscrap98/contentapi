using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ContentSearch : BaseContentSearch
    {
        public string Keyword {get;set;}
        public string Type {get;set;}
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

        //protected VoteService voteService;

        public ContentViewSource(ILogger<ContentViewSource> logger, IMapper mapper, IEntityProvider provider) //, VoteService voteService) 
            : base(logger, mapper, provider) 
        { 
            //this.voteService = voteService;
        }

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
            
            //votes are special: they can only be read
            //view.votes.@public = package.Relations.Where(x => x.type.StartsWith(Keys.VoteRelation))
            //    .ToDictionary(x => x.entityId1.ToString(), y => new VoteData() { vote = y.type == Keys.UpvoteRelation ? 1 : -1, date = y.createDateProper() });

            //view.votes.up = view.votes.@public.Count(x => x.Value.vote > 0);
            //view.votes.down = view.votes.@public.Count(x => x.Value.vote < 0);

            return view;
        }

        public override EntitySearch CreateSearch(ContentSearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += (search.Type ?? "%");
            return es;
        }

        public override IQueryable<EntityGroup> ModifySearch(IQueryable<EntityGroup> query, ContentSearch search)
        {
            query = base.ModifySearch(query, search);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
                query = LimitByValue(query, Keys.KeywordKey, search.Keyword);
            
            return query;
        }

        public override IQueryable<long> FinalizeQuery(IQueryable<EntityGroup> query, ContentSearch search)
        {
            var condense = query.GroupBy(MainIdSelector).Select(x => new ContinuousSort() { id = x.Key, sort = 0 });
            bool includeVotes = false;
            bool includeWatches = false;

            if(search.Sort == "votes")
            {
                includeVotes = true;
            }
            else if (search.Sort == "watches")
            {
                includeWatches = true;
            }
            else if (search.Sort == "score")
            {
                includeVotes = true;
                includeWatches = true;
            }

            if(includeVotes)
            {
                foreach(var voteWeight in Votes.VoteWeights)
                    condense = ApplyAdditionalSort<EntityRelation>(condense, x => x.entityId2, x => x.type == Keys.VoteRelation && x.value == voteWeight.Key, voteWeight.Value);
            }
            if(includeWatches)
            {
                condense = ApplyAdditionalSort<EntityRelation>(condense, x => -x.entityId2, x => x.type == Keys.WatchRelation, 1);
            }

            if(includeVotes || includeWatches)
            {
                if(search.Reverse)
                    condense = condense.OrderByDescending(x => x.sort);
                else
                    condense = condense.OrderBy(x => x.sort);
                
                return condense.Select(x => x.id);
            }

            return base.FinalizeQuery(query, search);
        }
    }
}