using System.Collections.Generic;
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

        //public async Task<string> GenerateNewReadonlyKey()
        //{
        //}

        public override async Task<FileView> CleanViewUpdateAsync(FileView view, EntityPackage existing, Requester requester)
        {
            var result = await base.CleanViewGeneralAsync(view, requester);

            //Always restore the filetype, you can't change uploaded files anyway.
            result.fileType = existing.Entity.content;
            //result.readonlyKey = existing.GetValue(Keys.ReadonlyKeyKey).value;

            //if(!existing.HasValue(Keys.ReadonlyKeyKey))
            //    existing.Add(new EntityValue() { });

            return result;
        }

        //public override Task<List<FileView>> PreparedSearchAsync(FileSearch search, Requester requester)
        //{
        //    return base.PreparedSearchAsync(search, requester);
        //}
    }
}