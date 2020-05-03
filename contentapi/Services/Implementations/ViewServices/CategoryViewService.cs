using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class CategorySearch : BaseContentSearch { }

    public class CategoryViewService : ViewServicePermissionbase<CategoryView, CategorySearch>
    {
        public CategoryViewService(ViewServices services, ILogger<ViewServiceBase<CategoryView, CategorySearch>> logger) 
            : base(services, logger) { }

        protected override string EntityType => keys.CategoryType;
        protected override string ParentType => keys.CategoryType;

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

        protected override Task<CategoryView> CleanViewGeneralAsync(CategoryView view, long userId)
        {
            //Always fail unless super, nobody can write categories etc.
            FailUnlessSuper(userId);
            return base.CleanViewGeneralAsync(view, userId);
        }

        protected override async Task<EntityPackage> DeleteCheckAsync(long id, long userId)
        {
            var package = await base.DeleteCheckAsync(id, userId);
            FailUnlessSuper(userId); //Also only super users can delete
            return package;
        }

        public override async Task<IList<CategoryView>> SearchAsync(CategorySearch search, ViewRequester requester)
        {
            logger.LogDebug($"Category GetAsync called by {requester}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var perms = BasicReadQuery(requester.userId, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            return await ViewResult(FinalizeQuery(perms, entitySearch));
        }
    }
}