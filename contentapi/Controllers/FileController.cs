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

namespace contentapi.Controllers
{
    //TODO: Many of these permission searches are the same (parentids). 
    //Fix up some of this to be generic?
    public class FileSearch : EntitySearchBase
    {
        public string Name {get;set;}
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class FileControllerProfile : Profile
    {
        public FileControllerProfile()
        {
            CreateMap<FileSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Name));
        }
    }

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

        protected override EntityPackage CreateBasePackage(FileView view)
        {
            return NewEntity(view.name, view.fileType);
        }

        protected override FileView CreateBaseView(EntityPackage package)
        {
            return new FileView() { name = package.Entity.name, fileType = package.Entity.content };
        }

        protected string GetPath(long id, int size = 0)
        {
            var name = id.ToString();

            if(size > 0)
                name += $"_{size}";

            return Path.Join(config.Location, name);
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
                var finalLocation = GetPath(newView.id);

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

        //protected 

        [HttpGet("raw/{id}")]
        public async Task<IActionResult> GetFileAsync([FromRoute]long id, [FromQuery]int size = 0)
        {
            //Need to read file and potentially resize it.
            if(size > 0)
            {
                if (size < 10)
                    return BadRequest("Too small!");
                else if (size < 100)
                    size = 10 * size / 10;
                else if (size < 1000)
                    size = 100 * size / 100;
                else
                    return BadRequest("Too large!");
            }

            //Go get that ONE file.
            var fileData = await provider.FindByIdAsync(id);

            if(fileData == null)
                return NotFound();
            
            if(!CanCurrentUser(keys.ReadAction, fileData))
                return NotFound(); //This needs to be the norm. Fix it!
            
            //Ok NOW we can go get it. We may need to perform a resize beforehand if we can't find the file.
            var finalLocation = GetPath(fileData.Entity.id, size);
            Stream stream = null;

            if(System.IO.File.Exists(finalLocation))
            {
                stream = System.IO.File.OpenRead(finalLocation);
            }
            else
            {
                return BadRequest("Couldn't find file (yet)");
            }

            return File(stream, fileData.Entity.content);
                //return Forbid("You don't have access to this file");

            //var search = new FileSearch();
            //search 
            //var view = (await GetViewsAsync(new FileSearch()));
        }

        protected async Task<List<FileView>> GetViewsAsync(FileSearch search)
        {
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var user = GetRequesterUidNoFail();

            var perms = BasicPermissionQuery(user, entitySearch);

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