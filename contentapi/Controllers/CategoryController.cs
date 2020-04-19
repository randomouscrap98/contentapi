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

    public class CategoryController : BasePermissionController<CategoryView>
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
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var user = GetRequesterUidNoFail();

            var perms = BasicPermissionQuery(user, entitySearch);
            var idHusk = ConvertToHusk(perms);

            return await ViewResult(FinalizeHusk<Entity>(idHusk, entitySearch));
        }

        protected override Task<CategoryView> CleanViewGeneralAsync(CategoryView view)
        {
            //Always fail unless super, nobody can write categories etc.
            FailUnlessRequestSuper();
            return base.CleanViewGeneralAsync(view);
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<CategoryView>> PostAsync([FromBody]CategoryView view)
        {
            view.id = 0;
            return ThrowToAction(() => WriteViewAsync(view));
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<CategoryView>> PutAsync([FromRoute] long id, [FromBody]CategoryView view)
        {
            view.id = id;
            return ThrowToAction(() => WriteViewAsync(view));
        }

        //[HttpDelete("{id}")]
        //[Authorize]
        //public Task<ActionResult<CategoryView>> DeleteAsync([FromRoute]long id)
        //{
        //    EntityPackage result = null;

        //    return ThrowToAction<CategoryView>(async () => 
        //    {
        //        result = await DeleteEntityCheck(id);
        //    },
        //    async() =>
        //    {
        //        await DeleteEntity(id);
        //        return ConvertToView(result);
        //    });
        //}
    }
}