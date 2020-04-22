using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Randomous.EntitySystem;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace contentapi.Controllers
{
    public class FileSearch : BaseContentSearch { }

    public class FileControllerConfig
    {
        public string Location {get;set;}
        public int MaxSize {get;set;}
    }

    public class FileController : BasePermissionController<FileView>
    {
        protected FileControllerConfig config;

        public FileController(ControllerServices services, ILogger<FileController> logger, IOptionsMonitor<FileControllerConfig> config) : base(services, logger)
        {
            this.config = config.CurrentValue;
        }

        protected override string ParentType => keys.UserType;
        protected override string EntityType => keys.FileType;
        protected override bool AllowOrphanPosts => true;

        protected override EntityPackage CreateBasePackage(FileView view)
        {
            return NewEntity(view.name, view.fileType);
        }

        protected override FileView CreateBaseView(EntityPackage package)
        {
            return new FileView() { name = package.Entity.name, fileType = package.Entity.content };
        }

        protected string GetPath(long id, GetFileModify modify = null)//int size = 0)
        {
            var name = id.ToString();

            if(modify != null)
            {
                var extraFolder = "_";

                if(modify.size > 0)
                    extraFolder += $"{modify.size}";
                if(modify.square)
                    extraFolder += "a";

                return Path.Join(config.Location, extraFolder, name);
            }

            return Path.Join(config.Location, name);
        }

        //Convert path to appropriate etag
        protected string GetETag(string path)
        {
            return path.Replace(config.Location, "").Replace("\\", "Z").Replace("/", "Z");
        }

        protected string GetAndMakePath(long id, GetFileModify modify = null)
        {
            var result = GetPath(id, modify);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(result));
            return result;
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<FileView>> UploadFile(IFormFile file)
        {
            return ThrowToAction(async ()=>
            {
                if(file.Length == 0)
                    throw new BadRequestException("No data uploaded!");
                
                if(file.Length > config.MaxSize)
                    throw new BadRequestException("File too large!");
                
                var newView = new FileView();
                newView.permissions["0"] = "R";

                IImageFormat format = null;

                //This will throw an exception if it's not an image (most likely)
                using(var image = Image.Load(file.OpenReadStream(), out format))
                {
                    newView.fileType = format.DefaultMimeType;
                }

                //We HAVE to write now to reserve an ID (and we found the mime type)
                newView = await WriteViewAsync(newView);
                var finalLocation = GetAndMakePath(newView.id);

                try
                {
                    //Now just copy to the filesystem?
                    using (var stream = System.IO.File.Create(finalLocation))
                    {
                        await file.CopyToAsync(stream);
                    }
                }
                catch
                {
                    //Delete that file we added oops
                    await DeleteByIdAsync(newView.id);
                    throw;
                }

                //The view is already done.
                return newView;
            });
        }

        protected override async Task<FileView> CleanViewUpdateAsync(FileView view, EntityPackage existing)
        {
            var result = await base.CleanViewGeneralAsync(view);

            //Always restore the filetype, you can't change uploaded files anyway.
            result.fileType = existing.Entity.content;

            return result;
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<FileView>> PutAsync([FromRoute] long id, [FromBody]FileView view)
        {
            view.id = id;
            return ThrowToAction(() => WriteViewAsync(view));
        }

        public class GetFileModify
        {
            public int size {get;set;}
            public bool square {get;set;}
        }

        [HttpGet("raw/{id}")]
        [ResponseCache(Duration=13824000)] //six months
        public async Task<IActionResult> GetFileAsync([FromRoute]long id, [FromQuery]GetFileModify modify)
        {
            if(modify.size > 0)
            {
                if (modify.size < 10)
                    return BadRequest("Too small!");
                else if (modify.size <= 100)
                    modify.size = 10 * (modify.size / 10);
                else if (modify.size <= 1000)
                    modify.size = 100 * (modify.size / 100);
                else
                    return BadRequest("Too large!");
            }

            //Go get that ONE file.
            var fileData = await provider.FindByIdAsync(id);

            if(fileData == null)
                return NotFound();
            
            if(!CanCurrentUser(keys.ReadAction, fileData))
                return NotFound(); //This needs to be the norm. Fix it!
            
            var finalPath = GetAndMakePath(fileData.Entity.id, modify); //new Func<string>(() => GetPath(fileData.Entity.id, modify));
            
            //Ok NOW we can go get it. We may need to perform a resize beforehand if we can't find the file.
            if(!System.IO.File.Exists(finalPath))
            {
                var baseImage = GetPath(fileData.Entity.id);
                IImageFormat format;

                await Task.Run(() =>
                {
                    using (var image = Image.Load(baseImage, out format))
                    {
                        var maxDim = Math.Max(image.Width, image.Height);
                        var minDim = Math.Min(image.Width, image.Height);

                        //Square ALWAYS happens, it can happen before other things.
                        if(modify.square)
                            image.Mutate(x => x.Crop(new Rectangle((image.Width - minDim)/2, (image.Height - minDim)/2, minDim, minDim)));

                        if(modify.size > 0 && !(format.DefaultMimeType == "image/gif" && (modify.size > image.Width || modify.size > image.Height)))
                        {
                            var width = 0;
                            var height = 0;

                            //Preserve aspect ratio when not square
                            if (image.Width > image.Height)
                                width = modify.size;
                            else
                                height = modify.size;

                            image.Mutate(x => x.Resize(width, height));
                        }

                        using (var stream = System.IO.File.OpenWrite(finalPath))
                        {
                            image.Save(stream, format);
                        }
                    }
                });
            }

            Response.Headers.Add("ETag", GetETag(finalPath));
            return File(System.IO.File.OpenRead(finalPath), fileData.Entity.content);
        }

        protected async Task<List<FileView>> GetViewsAsync(FileSearch search)
        {
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var user = GetRequesterUidNoFail();

            var perms = BasicReadQuery(user, entitySearch);

            if(search.ParentIds.Count > 0)
                perms = WhereParents(perms, search.ParentIds);

            var idHusk = ConvertToHusk(perms);

            return await ViewResult(FinalizeHusk<Entity>(idHusk, entitySearch));
        }

        [HttpGet]
        public async Task<ActionResult<List<FileView>>> GetAsync([FromQuery]FileSearch search)
        {
            return await GetViewsAsync(search);
        }
    }
}