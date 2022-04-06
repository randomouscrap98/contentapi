using System.Runtime.ExceptionServices;
using contentapi.Main;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class FileController : BaseController
{
   protected IFileService service;


   public FileController(BaseControllerServices services, IFileService service)
         : base(services)
   {
      this.service = service;
   }

   public class UploadFileModel : UploadFileConfig
   {
      public IFormFile? file {get;set;} = null;
      public List<IFormFile>? files = null;
   }

   [HttpPost]
   [Authorize]
   public async Task<ActionResult<ContentView>> UploadFile([FromForm] UploadFileModel model)
   {
      //File ALWAYS takes precedence, but have a nice fallback.
      if (model.file == null)
      {
         if (model.files != null && model.files.Count > 0)
            model.file = model.files[0];
         else
            return BadRequest("No file or files form data found!");
      }

      return await MatchExceptions(() =>
      {
         RateLimit(RateFile);
         var requester = GetUserIdStrict();
         return service.UploadFile(model, model.file.OpenReadStream(), requester);
      });
   }

   [HttpGet("raw/{hash}")]
   [ResponseCache(Duration = 13824000)] //six months
   public async Task<ActionResult<bool>> GetFileAsync([FromRoute] string hash, [FromQuery] GetFileModify modify)
   {
      Response.Headers.Add("ETag", hash);

      try
      {
         var result = await service.GetFileAsync(hash, modify);
         return File(result.Item1, result.Item2);
      }
      catch(Exception ex)
      {
         return await MatchExceptions(() =>
         {
            ExceptionDispatchInfo.Capture(ex).Throw();
            return Task.FromResult(true);
         });
      }
   }
}