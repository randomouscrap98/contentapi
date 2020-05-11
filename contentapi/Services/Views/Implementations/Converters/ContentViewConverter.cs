using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class ContentViewConvter : BasePermissionViewConverter, IViewConverter<ContentView, EntityPackage>
    {
        public EntityPackage FromView(ContentView view)
        {
            var package = NewEntity(view.name, view.content);
            ApplyFromViewPermissive(view, package, Keys.ContentType);

            foreach(var v in view.values)
            {
                package.Add(new EntityValue()
                {
                    entityId = view.id,
                    key = Keys.AssociatedValueKey + v.Key, 
                    value = v.Value,
                    createDate = null
                });
            }
            
            //Bad coding, too many dependencies. We set the type without the base because someone else will do it for us.
            package.Entity.type = view.type;

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