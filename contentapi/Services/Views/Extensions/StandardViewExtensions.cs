using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Extensions
{
    public static class StandardViewExtensions
    {
        public static void ApplyFromStandard<V,T>(this IViewConverter<V,T> converter, StandardView view, EntityPackage package, string type)
        {
            converter.ApplyFromEditView(view, package, type);
            converter.ApplyFromPermissionView(view, package, type);
            converter.ApplyFromValueView(view, package, type);
        }

        public static void ApplyToStandard<V,T>(this IViewConverter<V,T> converter, EntityPackage package, StandardView view)
        {
            converter.ApplyToEditView(package, view);
            converter.ApplyToPermissionView(package, view);
            converter.ApplyToValueView(package, view);
        }
    }    
}