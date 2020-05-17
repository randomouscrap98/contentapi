using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CommentSearch : BaseParentSearch
    {
        public List<long> UserIds {get;set;}
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

    public class CommentViewSource : BaseRelationViewSource, IViewSource<CommentView, EntityRelationPackage, EntityGroup, CommentSearch>
    {
        public override string EntityType => Keys.CommentHack;

        public CommentViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
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

        public CommentView ToView(EntityRelationPackage package)
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

        public EntityRelationPackage FromView(CommentView view)
        {
            //WARN: this isn't PRECISELY correct!!! But it's basically impossible to produce a 
            //proper comment just from a view!
            return new EntityRelationPackage() {Main = FromViewSimple(view)};
        }

        public async Task<List<EntityRelationPackage>> LinkAsync(IEnumerable<EntityRelation> relations)
        {
            //This finds historical data (if there is any, it's probably none every time)
            var secondarySearch = new EntityRelationSearch();
            secondarySearch.EntityIds1 = relations.Select(x => -x.id).ToList();

            var historyRelations = await provider.GetEntityRelationsAsync(secondarySearch);

            return relations.Select(x => new EntityRelationPackage()
            {
                Main = x,
                Related = historyRelations.Where(y => y.entityId1 == -x.id).ToList()
            }).ToList();
        }

        public IQueryable<long> SearchIds(CommentSearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search, x => x.entityId1);

            if(modify != null)
                query = modify(query);
            
            return FinalizeQuery(query, search, x => x.relation.id);
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public Task<List<EntityRelationPackage>> RetrieveAsync(IQueryable<long> ids)
        {
            return LinkAsync(GetByIds<EntityRelation>(ids));
        }
    }
}