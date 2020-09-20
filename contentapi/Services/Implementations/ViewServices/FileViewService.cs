using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

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
            var result = await base.CleanViewGeneralAsync(view, requester);

            //Always restore the filetype, you can't change uploaded files anyway.
            result.fileType = existing.Entity.content;

            return result;
        }
    }
}