using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
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

    public class ContentControllerProfile : Profile
    {
        public ContentControllerProfile()
        {
            CreateMap<ContentSearch, EntitySearch>()
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.Type));
                //Can't do keyword, it's special search
        }
    }

    public class ContentViewService : BasePermissionViewService<ContentView, ContentSearch>
    {
        protected CategoryViewService categoryService;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        public ContentViewService(ViewServices services, ILogger<ContentViewService> logger, CategoryViewService categoryService) 
            : base(services, logger) 
        { 
            this.categoryService = categoryService;
        }

        public override string EntityType => keys.ContentType;
        public override string ParentType => null;

        public async Task ControllerSetupAsync()
        {
            var categories = await categoryService.SearchAsync(new CategorySearch(), new Requester() { system = true });
        }
        
        public override bool CanUser(Requester requester, string action, EntityPackage package)
        {
            var result = base.CanUser(requester, action, package);

            if(cachedSupers == null)
            {
                logger.LogWarning("CanUser called without cached supers");
            }
            else
            {
                result = action != keys.ReadAction && cachedSupers[package.GetRelation(keys.ParentRelation).entityId1].Contains(requester.userId);
            }

            return result;
        }

        public override EntityPackage CreateBasePackage(ContentView view)
        {
            var package = NewEntity(view.name, view.content);

            //Need to add LOTS OF CRAP
            foreach(var keyword in view.keywords)
                package.Add(NewValue(keys.KeywordKey, keyword));
            
            foreach(var v in view.values)
                package.Add(NewValue(keys.AssociatedValueKey + v.Key, v.Value));
            
            //Bad coding, too many dependencies. We set the type without the base because someone else will do it for us.
            package.Entity.type = view.type;

            return package;
        }

        public override ContentView CreateBaseView(EntityPackage package)
        {
            var view = new ContentView();
            view.name = package.Entity.name;
            view.content = package.Entity.content;
            view.type = package.Entity.type.Substring(EntityType.Length);

            foreach(var keyword in package.Values.Where(x => x.key == keys.KeywordKey))
                view.keywords.Add(keyword.value);
            
            foreach(var v in package.Values.Where(x => x.key.StartsWith(keys.AssociatedValueKey)))
                view.values.Add(v.key.Substring(keys.AssociatedValueKey.Length), v.value);

            return view; //view;
        }

        public override async Task<IList<ContentView>> SearchAsync(ContentSearch search, Requester requester)
        {
            logger.LogTrace($"Content SearchAsync called by {requester}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var initial = BasicReadQuery(requester, entitySearch);

            //Right now, entities are matched with very specific read relations and MAYBE some creator ones.
            //Be VERY CAREFUL, I get the feeling the entity count can get blown out of proportion with this massive join.

            if(search.ParentIds.Count > 0)
                initial = WhereParents(initial, search.ParentIds);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
            {
                initial = initial
                    .Join(provider.GetQueryable<EntityValue>(), e => e.entity.id, v => v.entityId, 
                          (e,v) => new EntityGroup() { entity = e.entity, relation = e.relation, value = v})
                    .Where(x => x.value.key == keys.KeywordKey && EF.Functions.Like(x.value.value, search.Keyword));
            }

            return await ViewResult(FinalizeQuery(initial, entitySearch), requester);
        }
    }
}