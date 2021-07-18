using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class FileSearch : BaseContentSearch 
    { 
        public string Bucket {get;set;}

        /// <summary>
        /// A special thing I have to do to let internal searches still use the permissions
        /// search BUT search any bucket
        /// </summary>
        public bool SearchAllBuckets = false;
    }

    public class FileViewSource : BaseStandardViewSource<FileView, EntityPackage, EntityGroup, FileSearch>
    {
        public override string EntityType => Keys.FileType;

        public FileViewSource(ILogger<FileViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) {}

        public override EntityPackage FromView(FileView view)
        {
            var package = this.NewEntity(view.name, view.fileType)
                //.Add(NewValue(Keys.ReadonlyKeyKey, view.readonlyKey))
                .Add(NewValue(Keys.BucketKey, view.bucket));
            this.ApplyFromStandard(view, package, Keys.FileType);
            return package;
        }

        public override FileView ToView(EntityPackage package)
        {
            var view = new FileView() { 
                name = package.Entity.name, 
                fileType = package.Entity.content 
            };

            this.ApplyToStandard(package, view);

            //if(package.HasValue(Keys.ReadonlyKeyKey))
            //    view.readonlyKey = package.GetValue(Keys.ReadonlyKeyKey).value;
            if(package.HasValue(Keys.BucketKey))
                view.bucket = package.GetValue(Keys.BucketKey).value;

            return view;
        }

        public override async Task<IQueryable<EntityGroup>> ModifySearch(IQueryable<EntityGroup> query, FileSearch search)
        {
            query = await base.ModifySearch(query, search);

            //Find the exact matches for the bucket
            if(!search.SearchAllBuckets)
            {
                query = from q in query
                        join v in await Q<EntityValue>() on q.entity.id equals v.entityId
                        where v.key == Keys.BucketKey && v.value == search.Bucket
                        select q;
            }

            //if(!string.IsNullOrEmpty(search.Bucket))
            //{
            //}
            //else
            //{
            //    //Find images that have specifically NO bucket
            //    query = from q in query
            //            join v in await Q<EntityValue>() on q.entity.id equals v.entityId
            //            where EF.Functions.Like(v.key, keyLike) && EF.Functions.Like(v.value, valueLike)
            //            select q;
            //}
            
            return query;
        }
    }
}