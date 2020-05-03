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
    public class CategorySearch : BaseContentSearch { }

    public class CategoryController : BaseSimpleController//BasePermissionActionController<CategoryView>
    {
        public CategoryController(Keys keys, ILogger<BaseSimpleController> logger) : base(keys, logger)
        {
        }

        //public CategoryController(ILogger<CategoryController> logger, ControllerServices services)
        //    : base(services, logger) { }

        //protected override string EntityType => keys.CategoryType;
        //protected override string ParentType => keys.CategoryType;

        [HttpPost]
        [Authorize]
        public Task<ActionResult<CategoryView>> PostAsync([FromBody]CategoryView view)
        {
            logger.LogInformation($"PostAsync called, {typeof(CategoryView)}");
            view.id = 0;
            return ThrowToAction(() => WriteViewAsync(view));
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<V>> PutAsync([FromRoute] long id, [FromBody]V view)
        {
            logger.LogInformation($"PutAsync called, {typeof(V)}({view.id})");
            view.id = id;
            return ThrowToAction(() => WriteViewAsync(view));
        }

        [HttpDelete("{id}")]
        [Authorize]
        public Task<ActionResult<V>> DeleteAsync([FromRoute]long id)
        {
            logger.LogInformation($"DeleteAsync called, {typeof(V)}({id})");
            return ThrowToAction(() => DeleteByIdAsync(id));
        }
        protected override EntityPackage CreateBasePackage(CategoryView view)
        {
            var package = NewEntity(view.name, view.description);

            foreach(var v in view.values)
                package.Add(NewValue(keys.AssociatedValueKey + v.Key, v.Value));
            
            foreach(var v in view.localSupers)
                package.Add(NewRelation(v, keys.SuperRelation));

            return package;
        }

        protected override CategoryView CreateBaseView(EntityPackage package)
        {
            var view = new CategoryView();
            view.name = package.Entity.name;
            view.description = package.Entity.content;

            foreach(var v in package.Values.Where(x => x.key.StartsWith(keys.AssociatedValueKey)))
                view.values.Add(v.key.Substring(keys.AssociatedValueKey.Length), v.value);
            
            foreach(var v in package.Relations.Where(x => x.type == keys.SuperRelation))
                view.localSupers.Add(v.entityId1);

            return view;
        }


        //ALL you need is get and post. And validation.
        [HttpGet]
        public async Task<ActionResult<List<CategoryView>>> GetAsync([FromQuery]CategorySearch search)
        {
            var user = GetRequesterUidNoFail();
            logger.LogDebug($"Category GetAsync called by {user}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var perms = BasicReadQuery(user, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            return await ViewResult(FinalizeQuery(perms, entitySearch));
        }

        protected override Task<CategoryView> CleanViewGeneralAsync(CategoryView view)
        {
            //Always fail unless super, nobody can write categories etc.
            FailUnlessRequestSuper();
            return base.CleanViewGeneralAsync(view);
        }

        protected override async Task<EntityPackage> DeleteCheckAsync(long id)
        {
            var package = await base.DeleteCheckAsync(id);
            //var content = provider.GetQueryable<Entity>().Where(x => x.type)
            FailUnlessRequestSuper(); //Also only super users can delete
            return package;
        }
    }
}