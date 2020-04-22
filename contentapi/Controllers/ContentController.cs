using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
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

    public class ContentController : BasePermissionActionController<ContentView>
    {
        public ContentController(ILogger<ContentController> logger, ControllerServices services)
            : base(services, logger) { }

        protected override string EntityType => keys.ContentType;
        protected override string ParentType => null; //keys.CategoryType;
        
        protected override EntityPackage CreateBasePackage(ContentView view)
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

        protected override ContentView CreateBaseView(EntityPackage package)
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

        [HttpGet]
        public async Task<ActionResult<List<ContentView>>> GetAsync([FromQuery]ContentSearch search)
        {
            var user = GetRequesterUidNoFail();
            logger.LogDebug($"Content GetAsync called by {user}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var initial = BasicPermissionQuery(user, entitySearch);

            //Right now, entities are matched with very specific read relations and MAYBE some creator ones.
            //Be VERY CAREFUL, I get the feeling the entity count can get blown out of proportion with this massive join.

            if(search.ParentIds.Count > 0)
                initial = WhereParents(initial, search.ParentIds);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
            {
                initial = initial
                    .Join(provider.GetQueryable<EntityValue>(), e => e.entity.id, v => v.entityId, 
                          (e,v) => new EntityREVGroup() { entity = e.entity, relation = e.relation, value = v})
                    .Where(x => x.value.key == keys.KeywordKey && EF.Functions.Like(x.value.value, search.Keyword));
            }

            var idHusk = ConvertToHusk(initial);

            return await ViewResult(FinalizeHusk<Entity>(idHusk, entitySearch));
        }
    }
}