using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class FileSearch : BaseContentSearch { }

    public class FileViewService : BasePermissionViewService<FileView, FileSearch>
    {
        public FileViewService(ViewServices services, ILogger<FileViewService> logger) 
            : base(services, logger) { }

        public override string ParentType => keys.UserType;
        public override string EntityType => keys.FileType;
        public override bool AllowOrphanPosts => true;

        public override EntityPackage CreateBasePackage(FileView view)
        {
            return NewEntity(view.name, view.fileType);
        }

        public override FileView CreateBaseView(EntityPackage package)
        {
            return new FileView() { name = package.Entity.name, fileType = package.Entity.content };
        }

        public override async Task<FileView> CleanViewUpdateAsync(FileView view, EntityPackage existing, long userId)
        {
            var result = await base.CleanViewGeneralAsync(view, userId);

            //Always restore the filetype, you can't change uploaded files anyway.
            result.fileType = existing.Entity.content;

            return result;
        }

        public override async Task<IList<FileView>> SearchAsync(FileSearch search, ViewRequester requester)
        {
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var perms = BasicReadQuery(requester.userId, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            return await ViewResult(FinalizeQuery(perms, entitySearch));
        }
    }
}