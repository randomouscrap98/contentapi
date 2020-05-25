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

        public class ContinuousSort<R>
        {
            public long id {get;set;}
            public double sum {get;set;}
            public R passthrough {get;set;}
        }

        public IQueryable<ContinuousSort<R>> ApplyAdditionalSort<R>(
            IQueryable<ContinuousSort<R>> query, 
            Expression<Func<R, long>> join, 
            Expression<Func<R, bool>> whereClause, 
            double modifier) where R : EntityBase
        {
            var joined = query
                .GroupJoin(Q<R>().Where(whereClause), x => x.id, join, (s,r) => new { s = s, r = r })
                .SelectMany(x => x.r.DefaultIfEmpty(), (x,y) => new ContinuousSort<R>() { id = x.s.id, sum = x.s.sum, passthrough = y});

            return  from j in joined
                    group j by j.id into g
                    select new ContinuousSort<R>() { id = g.Key, sum = g.Max(x => x.sum) + g.Sum(x => x.passthrough.id > 0 ? modifier : 0) };//modifier * g.Select(x => x.r).Distinct().Count() };
        }

        public class ContinuousSortCarrier<R> where R : EntityBase
        {
            public R r {get;set;}
            public double modifier {get;set;}
        }

        ////This is SO inefficient, ESPECIALLY when it gets stacked! So many joins! But it's because there's no
        ////group by where count in ef core... I think. I tried and it didn't work: 5/25/2020
        //public IQueryable<ContinuousSort> ApplyAdditionalSort<R>(
        //    IQueryable<ContinuousSort> query, 
        //    //Expression<Func<R, long>> join, 
        //    Expression<Func<ContinuousSortCarrier<R>, long>> join, 
        //    Expression<Func<R, bool>> whereClause, 
        //    double modifier) where R : EntityBase
        //{
        //    //var joined = query
        //    //    .GroupJoin(Q<R>().Where(whereClause), x => x.id, join, (s,r) => new { s = s, r = r, m = modifier })
        //    //    .SelectMany(x => x.r.DefaultIfEmpty(), (x,y) => new ContinuousSort() { id = x.s.id, sort = x.s.sort, modifier = y.id * modifier });

        //    //return  from j in joined
        //    //        group j by j.id into g
        //    //        select new ContinuousSort() { id = g.Key, sort = g.Max(x => x.sort) + g.Sum(x => x.modifier) };//modifier * g.Select(x => x.r).Distinct().Count() };

        //    var joined = query
        //        .GroupJoin(Q<R>().Where(whereClause).Select(x => new ContinuousSortCarrier<R>() { r = x, modifier = modifier}), x => x.id, join, (s,r) => new { s = s, r = r })
        //        .SelectMany(x => x.r.DefaultIfEmpty(), (x,y) => new ContinuousSort() { id = x.s.id, sum = x.s.sum, modifier = y.modifier });

        //    return  from j in joined
        //            group j by j.id into g
        //            select new ContinuousSort() { id = g.Key, sum = g.Max(x => x.sum) + g.Sum(x => x.modifier) };//modifier * g.Select(x => x.r).Distinct().Count() };
        //}

        public override IQueryable<long> FinalizeQuery(IQueryable<EntityGroup> query, ContentSearch search)
        {
            var condense = query.GroupBy(MainIdSelector).Select(x => new ContinuousSort<EntityRelation>() { id = x.Key, sum = 0 });
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
                    condense = ApplyAdditionalSort<EntityRelation>(condense, x => -x.entityId2, x => x.type == Keys.VoteRelation + voteWeight.Key, voteWeight.Value);
            }
            if(includeWatches)
            {
                condense = ApplyAdditionalSort<EntityRelation>(condense, x => -x.entityId2, x => x.type == Keys.WatchRelation, 1);
            }

            if(includeVotes || includeWatches)
            {
                var grouped = condense;
                    //from c in condense
                    //group c by c.id into g
                    //select new { id = g.Key, sort = g.Sum(x => x.modifier) };//modifier * g.Select(x => x.r).Distinct().Count() };

                if(search.Reverse)
                    grouped = grouped.OrderByDescending(x => x.sum);
                else
                    grouped = grouped.OrderBy(x => x.sum);
                
                return grouped.Select(x => x.id);
            }

            return base.FinalizeQuery(query, search);
        }
    }
}