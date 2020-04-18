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
    public class CategorySearch : EntitySearchBase
    {
        public string Name {get;set;}
    }

    public class CategoryControllerProfile : Profile
    {
        public CategoryControllerProfile()
        {
            CreateMap<CategorySearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Name));
        }
    }

    public class CategoryController : PermissionBaseController<CategoryView>
    {
        public CategoryController(ILogger<CategoryController> logger, ControllerServices services)
            : base(services, logger) { }

        protected override string EntityType => keys.CategoryType;
        protected override string ParentType => keys.CategoryType;
        
        protected override EntityPackage CreateBasePackage(CategoryView view)
        {
            return NewEntity(view.name, view.description);
        }

        protected override CategoryView CreateBaseView(EntityPackage package)
        {
            var view = new CategoryView();
            view.name = package.Entity.name;
            view.description = package.Entity.content;
            return view;
        }


        //ALL you need is get and post. And validation.
        [HttpGet]
        public async Task<ActionResult<List<CategoryView>>> GetAsync([FromQuery]CategorySearch search)
        {
            var entitySearch = (EntitySearch)(await ModifySearchAsync(services.mapper.Map<EntitySearch>(search)));

            var user = GetRequesterUidNoFail();

            var perms = BasicPermissionQuery(user, entitySearch);
            var idHusk = ConvertToHusk(perms);

            return await ViewResult(FinalizeHusk<Entity>(idHusk, entitySearch));
        }

        protected Task<ActionResult<CategoryView>> PostBase(CategoryView view)
        {
            return ThrowToAction(async() =>
            {
                //Only super users can create categories. Flat out.
                FailUnlessRequestSuper(); 
                view = await PostCleanAsync(view);
            }, 
            async () => 
            {
                return ConvertToView(await WriteViewAsync(view));
            });
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<CategoryView>> PostAsync([FromBody]CategoryView view)
        {
            view.id = 0;
            return PostBase(view);
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<CategoryView>> PostAsync([FromRoute] long id, [FromBody]CategoryView view)
        {
            view.id = id;
            return PostBase(view);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public Task<ActionResult<CategoryView>> DeleteAsync([FromRoute]long id)
        {
            EntityPackage result = null;

            return ThrowToAction<CategoryView>(async () => 
            {
                result = await DeleteEntityCheck(id);
            },
            async() =>
            {
                await DeleteEntity(id);
                return ConvertToView(result);
            });
        }
    }
}