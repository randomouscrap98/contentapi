using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class ContentViewConverter : BasePermissionViewConverter, IViewConverter<ContentView, EntityPackage>
    {
        public EntityPackage FromView(ContentView view)
        {
            var package = NewEntity(view.name, view.content);
            ApplyFromViewPermissive(view, package, Keys.ContentType);

            foreach(var v in view.keywords)
            {
                package.Add(new EntityValue()
                {
                    entityId = view.id,
                    key = Keys.KeywordKey, //Keys.AssociatedValueKey + v.Key, 
                    value = v,
                    createDate = null
                });
            }
            
            package.Entity.type += view.type;

            return package;
        }

        public ContentView ToView(EntityPackage package)
        {
            var view = new ContentView();
            ApplyToViewPermissive(package, view);

            view.name = package.Entity.name;
            view.content = package.Entity.content;
            view.type = package.Entity.type.Substring(Keys.ContentType.Length);

            foreach(var keyword in package.Values.Where(x => x.key == Keys.KeywordKey))
                view.keywords.Add(keyword.value);
            
            return view;
        }

    }
}