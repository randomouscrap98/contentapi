using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.ViewConversion;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class CategorySearch : BaseContentSearch { }

    public class CategoryViewService : BasePermissionViewService<CategoryView, CategorySearch>
    {
        public CategoryViewService(ViewServicePack services, ILogger<CategoryViewService> logger, CategoryViewConverter converter) 
            : base(services, logger, converter) { }

        public override string EntityType => Keys.CategoryType;
        public override string ParentType => Keys.CategoryType;

        //public override EntityPackage CreateBasePackage(CategoryView view)
        //{
        //    var package = NewEntity(view.name, view.description);

        //    foreach(var v in view.values)
        //        package.Add(NewValue(Keys.AssociatedValueKey + v.Key, v.Value));
        //    
        //    foreach(var v in view.localSupers)
        //        package.Add(NewRelation(v, Keys.SuperRelation));

        //    return package;
        //}

        //public override CategoryView CreateBaseView(EntityPackage package)
        //{
        //    var view = new CategoryView();
        //    view.name = package.Entity.name;
        //    view.description = package.Entity.content;

        //    foreach(var v in package.Values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
        //        view.values.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);
        //    
        //    foreach(var v in package.Relations.Where(x => x.type == Keys.SuperRelation))
        //        view.localSupers.Add(v.entityId1);

        //    return view;
        //}

        //public override Task<CategoryView> CleanViewGeneralAsync(CategoryView view, Requester requester)
        //{
        //    //Always fail unless super, nobody can write categories etc.
        //    FailUnlessSuper(requester);
        //    return base.CleanViewGeneralAsync(view, requester);
        //}

        public override async Task<EntityPackage> DeleteCheckAsync(long id, Requester requester)
        {
            var package = await base.DeleteCheckAsync(id, requester);
            FailUnlessSuper(requester); //Also only super users can delete
            return package;
        }

        public override async Task<IList<CategoryView>> SearchAsync(CategorySearch search, Requester requester)
        {
            logger.LogTrace($"Category SearchAsync called by {requester}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var perms = BasicReadQuery(requester, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            return await ViewResult(FinalizeQuery(perms, entitySearch), requester);
        }
    }
}