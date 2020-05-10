using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Mapping
{
    public class CategoryMapper : BaseHistoricMapper, IViewConverter<CategoryView, EntityPackage>
    {
        public EntityPackage FromView(CategoryView view)
        {
            throw new System.NotImplementedException();
        }

        public CategoryView ToView(EntityPackage package)
        {
            var view = new CategoryView();

            ApplyToViewHistoric(package, view);

            return view;
        }
    }
}