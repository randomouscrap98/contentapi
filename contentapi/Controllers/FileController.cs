using System.Runtime.ExceptionServices;
using System.Web;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Main;
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

   public class UploadFileModel : UploadFileConfigExtra
   {
      public IFormFile? file {get;set;} = null;
      public List<IFormFile>? files = null;
   }

   public class UploadFileLowModel
   {
      public IFormFile? file {get;set;} = null;
      public string token {get;set;} = "";
      public string? successUrl {get;set;} = null;
      public string? errorUrl {get;set;} = null;
   }

   public class UploadFileObject : UploadFileConfig
   {
      public string base64blob {get;set;} = "";
      public ContentView @object {get;set;} = new ContentView();
   }

   [HttpGet("status")]
   public async Task<ActionResult<object>> GetStatus([FromQuery]int seconds = 60)
   {
      return await MatchExceptions(() =>
      {
         return Task.FromResult(service.GetImageLog(TimeSpan.FromSeconds(seconds)));
      });
   }

   [HttpPost("low")]
   //Use bool as a placeholder: it's really going to return some redirects and stuff
   public async Task<ActionResult<bool>> UploadFileLow([FromForm] UploadFileLowModel model)
   {
      try
      {
         if(model.file == null)
            return BadRequest("No file form data found!");
         
         //OK now we have a userId (probably).
         var userId = GetUserIdFromToken(model.token);

         RateLimit(RateFile);

         var newModel = new UploadFileConfigExtra { }; //All default for now?
         var result = await service.UploadFile(newModel, model.file.OpenReadStream(), userId);

         //Ok guess it was fine. Now we... redirect to the success url? Otherwise redirect to the link
         var url = model.successUrl ?? GetRoute(nameof(GetFileAsync)) ?? 
            throw new InvalidOperationException("No success URL found (programming error!)");
         
         //Unless something is terribly wrong, hash is always url safe
         url = url.Replace("{{hash}}", result.hash)
                  .Replace("{{id}}", result.id.ToString());
         
         return Redirect(url);
      }
      catch(Exception ex)
      {
         services.logger.LogError($"ERROR DURING FILE UPLOAD: {ex}");

         if(!string.IsNullOrWhiteSpace(model.errorUrl))
         {
            string url = model.errorUrl;
            url = url.Replace("{{error}}", HttpUtility.UrlEncode(ex.Message));
            return Redirect(url);
         }
         else 
         {
            return BadRequest("Error during file upload: " + ex.Message);
         }
      }
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

   [HttpPost("asobject")]
   [Authorize]
   public async Task<ActionResult<ContentView>> UploadFile([FromBody] UploadFileObject fileData)
   {
      return await MatchExceptions(() =>
      {
         RateLimit(RateFile);

         //This one we must report, because it'll give the wrong impression if we let it slide
         if(fileData.@object.id != 0)
            throw new RequestException("You must upload NEW content (nonzero id)");
         
         //But fix this one for them regardless
         fileData.@object.contentType = InternalContentType.file;

         using var memstream = new MemoryStream(Convert.FromBase64String(fileData.base64blob));
         
         var requester = GetUserIdStrict();
         return service.UploadFile(fileData.@object, fileData, memstream, requester);
      });
   }

   [HttpGet("raw/{hash}")]
   [ResponseCache(Duration = Constants.GeneralCacheAge)] 
   public async Task<ActionResult<bool>> GetFileAsync([FromRoute] string hash, [FromQuery] GetFileModify modify)
   {
      //This is fine because files contents will never change, BUT isn't appropriate for most other things!
      Response.Headers.Add("ETag", hash);

      try
      {
         var result = await service.GetFileAsync(hash, modify);
         return File(result.Item1, result.Item2);
      }
      catch(Exception ex)
      {
         services.logger.LogError($"Exception during file retrieve: {ex}");

         return await MatchExceptions(() =>
         {
            ExceptionDispatchInfo.Capture(ex).Throw();
            return Task.FromResult(true);
         });
      }
   }
}