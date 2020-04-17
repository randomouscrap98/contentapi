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
    public class ContentSearch : EntitySearchBase
    {
        public string Title {get;set;}
        public string Keyword {get;set;}
        public string Type {get;set;}
        public List<long> CategoryIds {get;set;} = new List<long>();
    }

    public class ContentControllerProfile : Profile
    {
        public ContentControllerProfile()
        {
            CreateMap<ContentSearch, EntitySearch>()
                .ForMember(x => x.NameLike, o => o.MapFrom(s => s.Title))
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.Type));
        }
    }

    public class ContentController : PermissionBaseController<ContentView>
    {
        public ContentController(ILogger<ContentController> logger, ControllerServices services)
            : base(services, logger) { }

        protected override string EntityType => keys.ContentType;
        protected override string ParentType => keys.CategoryType;
        
        protected override EntityPackage CreateBasePackage(ContentView view)
        {
            var package = NewEntity(view.title, view.content);

            //Need to add LOTS OF CRAP
            foreach(var keyword in view.keywords)
                package.Add(NewValue(keys.KeywordKey, keyword));
            
            foreach(var v in view.values)
                package.Add(NewValue(TypeSet(v.Key, keys.AssociatedValueKey), v.Value));
            
            //Bad coding, too many dependencies. We set the type without the base because someone else will do it for us.
            package.Entity.type = view.type;

            return package;
        }

        protected override ContentView CreateBaseView(EntityPackage package)
        {
            var view = new ContentView();
            view.title = package.Entity.name;
            view.content = package.Entity.content;
            view.type = TypeSub(package.Entity.type, EntityType);

            foreach(var keyword in package.Values.Where(x => x.key == keys.KeywordKey))
                view.keywords.Add(keyword.value);
            
            foreach(var v in package.Values.Where(x => TypeIs(x.key, keys.AssociatedValueKey)))
                view.values.Add(TypeSub(v.key, keys.AssociatedValueKey), v.value);

            return view; //view;
        }

        [HttpGet]
        public async Task<ActionResult<List<ContentView>>> GetAsync([FromQuery]ContentSearch search)
        {
            var entitySearch = (EntitySearch)(await ModifySearchAsync(services.mapper.Map<EntitySearch>(search)));

            var user = GetRequesterUidNoFail();

            var initial = BasicPermissionQuery(user, entitySearch);

            //Right now, entities are matched with very specific read relations and MAYBE some creator ones.
            //Be VERY CAREFUL, I get the feeling the entity count can get blown out of proportion with this massive join.

            if(search.CategoryIds.Count > 0)
            {
                initial = initial
                    .Join(services.provider.GetQueryable<EntityRelation>(), e => e.entity.id, r => r.entityId2, 
                          (e,r) => new EntityRelationGroup() { entity = e.entity, relation = r})
                    .Where(x => x.relation.type == keys.ParentRelation && search.CategoryIds.Contains(x.relation.entityId1));
            }

            if(!string.IsNullOrWhiteSpace(search.Keyword))
            {
                initial = initial
                    .Join(services.provider.GetQueryable<EntityValue>(), e => e.entity.id, v => v.entityId, 
                          (e,v) => new EntityFullGroup() { entity = e.entity, relation = e.relation, value = v})
                    .Where(x => x.value.key == keys.KeywordKey && EF.Functions.Like(x.value.value, search.Keyword));
            }

            var idHusk = ConvertToHusk(initial);

            return await ViewResult(FinalizeHusk(idHusk, entitySearch));
        }

        protected async Task<ActionResult<ContentView>> PostBase(ContentView view)
        {
            try
            {
                view = await PostCleanAsync(view);
            }
            catch(AuthorizationException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return ConvertToView(await WriteViewAsync(view));
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<ContentView>> PostAsync([FromBody]ContentView view)
        {
            view.id = 0;
            return PostBase(view);
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<ContentView>> PutAsync([FromRoute] long id, [FromBody]ContentView view)
        {
            view.id = id;
            return PostBase(view);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<ContentView>> DeleteAsync([FromRoute]long id)
        {
            EntityPackage result = null;

            try
            {
                result = await DeleteEntityCheck(id);
            }
            catch(AuthorizationException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }

            await DeleteEntity(id);
            return ConvertToView(result);
        }
    }
}