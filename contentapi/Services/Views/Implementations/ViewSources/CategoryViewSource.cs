using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class CategorySearch : BaseContentSearch { }

    public class CategoryViewSource : BaseEntityViewSource, IViewSource<CategoryView, EntityPackage, EntityGroup, CategorySearch>
    {
        public override string EntityType => Keys.CategoryType;

        public CategoryViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public EntityPackage FromView(CategoryView view)
        {
            var package = this.NewEntity(view.name, view.description);
            this.ApplyFromStandard(view, package, Keys.CategoryType);

            foreach(var v in view.localSupers)
            {
                package.Add(new EntityRelation()
                {
                    entityId1 = v,
                    entityId2 = view.id,
                    createDate = null,
                    type = Keys.SuperRelation
                });
            }

            return package;
        }

        public CategoryView ToView(EntityPackage package)
        {
            var view = new CategoryView();
            this.ApplyToStandard(package, view);

            view.name = package.Entity.name;
            view.description = package.Entity.content;
            
            foreach(var v in package.Relations.Where(x => x.type == Keys.SuperRelation))
                view.localSupers.Add(v.entityId1);

            return view;
        }

        public IQueryable<long> SearchIds(CategorySearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search);

            if(search.ParentIds.Count > 0)
                query = LimitByParents(query, search.ParentIds);

            if(modify != null)
                query = modify(query);

            //Special sorting routines go here
            
            return FinalizeQuery(query, search, x => x.entity.id);
        }
    }
}