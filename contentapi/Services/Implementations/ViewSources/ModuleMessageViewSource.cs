using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ModuleMessageViewSearch : BaseSearch
    {
        public List<long> SenderIds {get;set;} = new List<long>();
        public List<long> ReceiverIds {get;set;} = new List<long>();

        public string ModuleLike {get;set;}
    }

    public class ModuleMessageViewSourceProfile : Profile
    {
        public ModuleMessageViewSourceProfile()
        {
            CreateMap<ModuleMessageViewSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.SenderIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ReceiverIds.Select(x => -x).ToList()))
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.ModuleLike));
        }
    }

    public class ModuleMessageViewSource : BaseViewSource<ModuleMessageView, EntityRelation, EntityGroup, ModuleMessageViewSearch>
    {
        public string EntityType => Keys.ModuleMessageKey;
        public override Expression<Func<EntityGroup, long>> MainIdSelector => x => x.relation.id;

        protected Regex userMatch = new Regex(@"%(\d+)%", RegexOptions.Compiled);

        public ModuleMessageViewSource(ILogger<ModuleMessageViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override ModuleMessageView ToView(EntityRelation relation)
        {
            var view = new ModuleMessageView();
            this.ApplyToBaseView(relation, view);
            view.sendUserId = relation.entityId1;
            view.receiveUserId = -relation.entityId2;
            view.message = relation.value;
            view.usersInMessage = userMatch.Matches(view.message).Select(x => long.Parse(x.Groups[1].Value)).ToList(); //x.Value.Trim("%".ToCharArray()))).ToList();
            view.module = relation.type.Substring(EntityType.Length);
            return view;
        }

        public override EntityRelation FromView(ModuleMessageView view)
        {
            var relation = new EntityRelation();
            this.ApplyFromBaseView(view, relation);
            relation.entityId1 = view.sendUserId;
            relation.entityId2 = -view.receiveUserId;
            relation.value = view.message;
            relation.type = EntityType + view.module;
            return relation;
        }

        public override IQueryable<EntityGroup> GetBaseQuery(ModuleMessageViewSearch search)
        {
            var relationSearch = mapper.Map<EntityRelationSearch>(search);
            relationSearch.TypeLike = EntityType + (search.ModuleLike ?? "%");

            return provider.ApplyEntityRelationSearch(Q<EntityRelation>(), relationSearch, false).Select(x => new EntityGroup() { relation = x });
        }

        public override Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }
    }
}