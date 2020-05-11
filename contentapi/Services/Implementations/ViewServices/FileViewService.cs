using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Mapping;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class FileSearch : BaseContentSearch { }

    public class FileViewService : BasePermissionViewService<FileView, FileSearch>
    {
        public FileViewService(ViewServicePack services, ILogger<FileViewService> logger, FileMapper converter) 
            : base(services, logger, converter) { }

        public override string ParentType => Keys.UserType;
        public override string EntityType => Keys.FileType;
        public override bool AllowOrphanPosts => true;

        //public override EntityPackage CreateBasePackage(FileView view)
        //{
        //    return NewEntity(view.name, view.fileType);
        //}

        //public override FileView CreateBaseView(EntityPackage package)
        //{
        //    return new FileView() { name = package.Entity.name, fileType = package.Entity.content };
        //}

        public override async Task<FileView> CleanViewUpdateAsync(FileView view, EntityPackage existing, Requester requester)
        {
            var result = await base.CleanViewGeneralAsync(view, requester);

            //Always restore the filetype, you can't change uploaded files anyway.
            result.fileType = existing.Entity.content;

            return result;
        }

        public override async Task<IList<FileView>> SearchAsync(FileSearch search, Requester requester)
        {
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var perms = BasicReadQuery(requester, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            return await ViewResult(FinalizeQuery(perms, entitySearch), requester);
        }
    }
}