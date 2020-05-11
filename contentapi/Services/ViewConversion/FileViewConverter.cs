using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.ViewConversion
{
    public class FileViewConverter : BasePermissionViewConverter, IViewConverter<FileView, EntityPackage>
    {
        public EntityPackage FromView(FileView view)
        {
            var package = NewEntity(view.name, view.fileType);
            ApplyFromViewPermissive(view, package, Keys.FileType);
            return package;
        }

        public FileView ToView(EntityPackage package)
        {
            var view = new FileView() { name = package.Entity.name, fileType = package.Entity.content };
            ApplyToViewPermissive(package, view);
            return view;
        }
    }
}