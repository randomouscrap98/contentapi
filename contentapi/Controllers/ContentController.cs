using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            if(search.CategoryIds.Count > 0)
                initial = initial.Where(x => search.CategoryIds.Contains(x.relation.entityId1));

            var idHusk = ConvertToHusk(initial);

            return await ViewResult(FinalizeHusk(idHusk, entitySearch));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ContentView>> PostAsync([FromBody]ContentView view)
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