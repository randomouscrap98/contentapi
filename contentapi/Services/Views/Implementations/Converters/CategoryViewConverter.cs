using System.Linq;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class CategoryViewConverter : BaseViewConverter , IViewConverter<CategoryView, EntityPackage>
    {
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
    }
}