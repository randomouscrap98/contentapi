using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class FileViewConverter : BaseViewConverter, IViewConverter<FileView, EntityPackage>
    {
        public EntityPackage FromView(FileView view)
        {
            var package = this.NewEntity(view.name, view.fileType);
            this.ApplyFromStandard(view, package, Keys.FileType);
            return package;
        }

        public FileView ToView(EntityPackage package)
        {
            var view = new FileView() { name = package.Entity.name, fileType = package.Entity.content };
            this.ApplyToStandard(package, view);
            return view;
        }
    }
}