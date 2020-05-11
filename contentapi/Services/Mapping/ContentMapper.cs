using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Mapping
{
    public class ContentMapper : BasePermissiveMapper, IViewConverter<ContentView, EntityPackage>
    {
        public EntityPackage FromView(ContentView view)
        {
            var package = NewEntity(view.name, view.content);
            ApplyFromViewPermissive(view, package, Keys.ContentType);

            //Need to add LOTS OF CRAP
            FromViewValues(view.values).ForEach(x =>
            {
                x.entityId = view.id;
                package.Add(x);
            });

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
            view.values = ToViewValues(package.Values);
            
            foreach(var v in package.Values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
                view.values.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);

            return view;
        }

    }
}