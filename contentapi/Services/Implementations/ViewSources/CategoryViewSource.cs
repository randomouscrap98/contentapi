using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class CategorySearch : BaseContentSearch { }

    public class CategoryViewSource : BaseStandardViewSource<CategoryView, EntityPackage, EntityGroup, CategorySearch>
    {
        public override string EntityType => Keys.CategoryType;

        public CategoryViewSource(ILogger<CategoryViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override EntityPackage FromView(CategoryView view)
        {
            var package = NewEntity(view.name, view.description);
                ApplyFromStandard(view, package, Keys.CategoryType);

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

        public override CategoryView ToView(EntityPackage package)
        {
            var view = new CategoryView();
            this.ApplyToStandard(package, view);

            view.name = package.Entity.name;
            view.description = package.Entity.content;
            
            foreach(var v in package.Relations.Where(x => x.type == Keys.SuperRelation))
                view.localSupers.Add(v.entityId1);

            return view;
        }
    }
}