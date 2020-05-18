using System;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class FileSearch : BaseContentSearch { }

    public class FileViewSource : BaseStandardViewSource<FileView, EntityPackage, EntityGroup, FileSearch>
    {
        public override string EntityType => Keys.FileType;

        public FileViewSource(ILogger<FileViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override EntityPackage FromView(FileView view)
        {
            var package = this.NewEntity(view.name, view.fileType);
            this.ApplyFromStandard(view, package, Keys.FileType);
            return package;
        }

        public override FileView ToView(EntityPackage package)
        {
            var view = new FileView() { name = package.Entity.name, fileType = package.Entity.content };
            this.ApplyToStandard(package, view);
            return view;
        }
    }
}