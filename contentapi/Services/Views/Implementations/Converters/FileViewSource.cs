using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class FileSearch : BaseContentSearch { }

    public class FileViewSource : BaseEntityViewSource , IViewSource<FileView, EntityPackage, EntityGroup, FileSearch>
    {
        public override string EntityType => Keys.FileType;

        public FileViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

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

        public IQueryable<long> SearchIds(FileSearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search);

            if(search.ParentIds.Count > 0)
                query = LimitByParents(query, search.ParentIds);

            if(modify != null)
                query = modify(query);

           //Special sorting routines go here

            return FinalizeQuery(query, search, x => x.entity.id);
        }

    }
}