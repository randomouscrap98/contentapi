using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class VoteSearch : BaseSearch
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();
        public string Vote {get;set;}
    }

    public class VoteViewSourceProfile : Profile 
    {
        public VoteViewSourceProfile()
        {
            //Only map direct fields which are the same. We lose contentid and other things... perhaps
            //they will be added later.
            CreateMap<VoteSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.UserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ContentIds.Select(x => -x).ToList()));
        }
    }

    public class VoteViewSource: BaseRelationViewSource<VoteView, EntityRelation, EntityGroup, VoteSearch>
    {
        public VoteViewSource(ILogger<VoteViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) {}

        public override string EntityType => Keys.VoteRelation;
        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => -x.entityId2;

        public override EntityRelation FromView(VoteView view)
        {
            var relation = new EntityRelation()
            {
                type = EntityType + view.vote,
                entityId1 = view.userId,
                entityId2 = -view.contentId
            };

            this.ApplyFromBaseView(view, relation);

            return relation;
        }

        public override VoteView ToView(EntityRelation basic)
        {
            var view = new VoteView()
            {
                vote = basic.type.Substring(Keys.VoteRelation.Length),
                userId = basic.entityId1,
                contentId = -basic.entityId2
            };

            this.ApplyToBaseView(basic, view);

            return view;
        }

        public override EntityRelationSearch CreateSearch(VoteSearch search)
        {
            var eSearch = base.CreateSearch(search);
            eSearch.TypeLike += (search.Vote?.ToLower() ?? "%");
            return eSearch;
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public override async Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return await services.provider.GetListAsync(await GetByIds<EntityRelation>(ids));
        }
    }
}