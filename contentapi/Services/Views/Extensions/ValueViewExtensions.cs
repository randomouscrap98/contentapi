using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    /// <summary>
    /// A permissive entity has permissions and a parent, essentially. Also assumed to be historic
    /// (why WOULDN'T you want to keep track of permission changes?!)
    /// </summary>
    public static class ValueViewExtensions
    {
        public static List<EntityValue> FromViewValues(Dictionary<string,string> values)
        {
            var result = new List<EntityValue>();

            foreach(var v in values)
            {
                result.Add(new EntityValue()
                {
                    key = Keys.AssociatedValueKey + v.Key,
                    createDate = null,
                    value = v.Value
                });
            }

            return result;
        }

        public static Dictionary<string,string> ToViewValues(IEnumerable<EntityValue> values)
        {
            var result = new Dictionary<string, string>();

            foreach(var v in values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
                result.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);

            return result;
        }

        public static void ApplyToValueView(this BaseViewConverter converter, EntityPackage package, IValueVlue view)
        {
            view.values = ToViewValues(package.Values);
        }

        public static void ApplyFromValueView(this BaseViewConverter converter, IValueVlue view, EntityPackage package, string type)
        {
            FromViewValues(view.values).ForEach(x => 
            {
                x.entityId = view.id;
                package.Add(x);
            });
        }
    }
}