using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.ViewConversion
{
    public class CategoryViewConverter : BasePermissionViewConverter, IViewConverter<CategoryView, EntityPackage>
    {
        public EntityPackage FromView(CategoryView view)
        {
            var package = NewEntity(view.name, view.description);
            ApplyFromViewPermissive(view, package, Keys.CategoryType);

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
            ApplyToViewPermissive(package, view);

            view.name = package.Entity.name;
            view.description = package.Entity.content;
            
            foreach(var v in package.Relations.Where(x => x.type == Keys.SuperRelation))
                view.localSupers.Add(v.entityId1);

            return view;
        }
    }
}