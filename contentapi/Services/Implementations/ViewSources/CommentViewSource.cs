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

namespace contentapi.Services.Implementations
{
    public class CommentSearch : BaseSearch //BaseParentSearch
    {
        public List<long> ParentIds {get;set;} = new List<long>();
        public List<long> UserIds {get;set;} = new List<long>();
        public IdLimiter ContentLimit {get;set;} = new IdLimiter();

        public string ContentLike {get;set;}
    }

    public class CommentViewSourceProfile : Profile
    {
        public CommentViewSourceProfile()
        {
            CreateMap<CommentSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.ParentIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.UserIds.Select(x => -x).ToList()));
                //We actually CAN map parent ids directly
        }
    }

    public class CommentViewSource : BaseRelationViewSource<CommentView, EntityRelationPackage, EntityGroup, CommentSearch>
    {
        public override string EntityType => Keys.CommentHack;
        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => x.entityId1;

        public CommentViewSource(ILogger<CommentViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public CommentView ToViewSimple(EntityRelation relation)
        {
            var view = new CommentView();
            this.ApplyToBaseView(relation, view);
            view.createUserId = -relation.entityId2;
            view.content = relation.value;
            view.parentId = relation.entityId1;

            //Assume (bad assume!) that these are OK values... we don't know if edit is even supported?
            view.editUserId = view.createUserId;
            view.editDate = view.createDate;

            return view;
        }

        public EntityRelation FromViewSimple(CommentView view)
        {
            var relation = new EntityRelation();
            this.ApplyFromBaseView(view, relation);
            relation.type = Keys.CommentHack;
            relation.value = view.content;
            relation.entityId1 = view.parentId;
            relation.entityId2 = -view.createUserId;
            return relation;
        }

        public override CommentView ToView(EntityRelationPackage package)
        {
            var view = ToViewSimple(package.Main);
            var orderedRelations = package.Related.OrderBy(x => x.id);
            var lastEdit = orderedRelations.LastOrDefault(x => x.type.StartsWith(Keys.CommentHistoryHack));
            var last = orderedRelations.LastOrDefault();

            if(lastEdit != null)
            {
                view.editDate = (DateTime)lastEdit.createDateProper();
                view.editUserId = -lastEdit.entityId2;
            }

            view.deleted = last != null && last.type.StartsWith(Keys.CommentDeleteHack);

            return view;
        }

        public override async Task<IQueryable<EntityGroup>> GetBaseQuery(CommentSearch search)
        {
            var baseQuery = await base.GetBaseQuery(search);

            if(!string.IsNullOrEmpty(search.ContentLike))
                baseQuery = baseQuery.Where(x => EF.Functions.Like(x.relation.value, search.ContentLike));

            return baseQuery;
        }

        public override EntityRelationPackage FromView(CommentView view)
        {
            //WARN: this isn't PRECISELY correct!!! But it's basically impossible to produce a 
            //proper comment just from a view!
            return new EntityRelationPackage() {Main = FromViewSimple(view)};
        }

        public async Task<List<EntityRelationPackage>> LinkAsync(IEnumerable<EntityRelation> relations)
        {
            //This finds historical data (if there is any, it's probably none every time)

            if(relations.Count() > 0)
            {
                var secondarySearch = new EntityRelationSearch();
                secondarySearch.EntityIds1 = relations.Select(x => -x.id).ToList();

                var historyRelations = await provider.GetEntityRelationsAsync(secondarySearch);

                return relations.Select(x => new EntityRelationPackage()
                {
                    Main = x,
                    Related = historyRelations.Where(y => y.entityId1 == -x.id).ToList()
                }).ToList();
            }
            else
            {
                return new List<EntityRelationPackage>(); //NOTHING
            }
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public override async Task<List<EntityRelationPackage>> RetrieveAsync(IQueryable<long> ids)
        {
            return await LinkAsync(await GetByIds<EntityRelation>(ids));
        }

        public override Task<IQueryable<long>> FinalizeQuery(IQueryable<EntityGroup> query, CommentSearch search)  
        {
            if(search.ContentLimit.Limit.Count > 0)
                return SimpleMultiLimit(query, search.ContentLimit.Limit, e => e.entityId1);

            return base.FinalizeQuery(query, search);
        }
    }
}