using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class FileViewService : BasePermissionViewService<FileView, FileSearch>
    {
        public FileViewService(ViewServicePack services, ILogger<FileViewService> logger, FileViewSource converter, BanViewSource banSource) 
            : base(services, logger, converter, banSource) { }

        public override string ParentType => Keys.UserType;
        public override string EntityType => Keys.FileType;
        public override bool AllowOrphanPosts => true;

        public override async Task<FileView> CleanViewUpdateAsync(FileView view, EntityPackage existing, Requester requester)
        {
            var result = await base.CleanViewUpdateAsync(view, existing, requester);
            var existingView = converter.ToView(existing);

            //Always restore the filetype, you can't change uploaded files anyway.
            if(!requester.system)
            {
                result.fileType = existingView.fileType; //.Entity.content;
                result.quantization = existingView.quantization; //.GetValue();
            }

            return result;
        }
    }
}