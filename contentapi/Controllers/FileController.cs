using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace contentapi.Controllers
{
   public class FileControllerConfig
   {
      public string Location { get; set; }
      public int MaxSize { get; set; }
      public string QuantizerProgram { get; set; }
      public TimeSpan QuantizeTimeout { get; set; } = TimeSpan.FromSeconds(20);
      public int MaxQuantize { get; set; } = 256;
      public int MinQuantize { get; set; } = 2;
   }

   public class FileController : BaseSimpleController
   {
      protected FileControllerConfig config;
      protected FileViewService service;
      protected ILanguageService docService;

      protected readonly SemaphoreSlim filelock = new SemaphoreSlim(1, 1);

      public FileController(BaseSimpleControllerServices services, FileControllerConfig config,
          FileViewService service, ILanguageService languageService)
          : base(services)
      {
         this.config = config;
         this.service = service;
         this.docService = languageService;
      }

      protected string GetPath(long id, GetFileModify modify = null)//int size = 0)
      {
         var name = id.ToString();

         if (modify != null)
         {
            var extraFolder = "_";

            if (modify.size > 0)
               extraFolder += $"{modify.size}";
            if (modify.crop)
               extraFolder += "a";
            if (modify.freeze)
               extraFolder += "f";

            if (extraFolder != "_")
               return Path.Join(config.Location, extraFolder, name);
         }

         return Path.Join(config.Location, name);
      }

      //Convert path to appropriate etag
      protected string GetETag(string path)
      {
         return path.Replace(config.Location, "").Replace("\\", "Z").Replace("/", "Z");
      }

      //Should be thread safe
      protected string GetAndMakePath(long id, GetFileModify modify = null)
      {
         var result = GetPath(id, modify);
         System.IO.Directory.CreateDirectory(Path.GetDirectoryName(result));
         return result;
      }

      [HttpPost]
      [Authorize]
      public Task<ActionResult<FileView>> UploadFile(IFormFile file = null, List<IFormFile> files = null,
          [FromQuery] bool tryresize = true, [FromQuery] string bucket = null, [FromQuery] int quantize = -1)
      {
         return ThrowToAction(async () =>
         {
            //File ALWAYS takes precedence, but have a nice fallback.
            if (file == null)
            {
               if (files != null && files.Count > 0)
                  file = files[0];
               else
                  throw new BadRequestException("No file or files form data found!");
            }

            if (file.Length == 0)
               throw new BadRequestException("No data uploaded!");

            var requester = GetRequesterNoFail();

            var newView = new FileView() { bucket = bucket };
            newView.permissions[0] = "R";

            IImageFormat format = null;
            long imageByteCount = file.Length;
            Stream imageStream = file.OpenReadStream();

            //This will throw an exception if it's not an image (most likely)
            using (var image = Image.Load(imageStream, out format))
            {
               newView.fileType = format.DefaultMimeType;

               double sizeFactor = 0.8;
               int width = image.Width;
               int height = image.Height;

               while (tryresize && imageByteCount > config.MaxSize && sizeFactor > 0)
               {
                  double resize = 1 / Math.Sqrt(imageByteCount / (config.MaxSize * sizeFactor));
                  logger.LogWarning($"User image too large ({imageByteCount}), trying ONE resize by {resize}");
                  width = (int)(width * resize);
                  height = (int)(height * resize);
                  image.Mutate(x => x.Resize(width, height, KnownResamplers.Lanczos3));

                  imageStream = new MemoryStream();
                  image.Save(imageStream, format);
                  imageByteCount = imageStream.Length;

                  //Keep targeting an EVEN more harsh error margin (even if it's incorrect because
                  //it stacks with previous resizes), also this makes the loop guaranteed to end
                  sizeFactor -= 0.2;
               }

               //If the image is still too big, bad ok bye
               if (imageByteCount > config.MaxSize)
                  throw new BadRequestException("File too large!");
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            logger.LogDebug($"New image size: {imageByteCount}");

            //We HAVE to write now to reserve an ID (and we found the mime type)
            newView = await service.WriteAsync(newView, requester);
            var finalLocation = GetAndMakePath(newView.id);

            try
            {
               await filelock.WaitAsync();

               try
               {
                  //Now just copy to the filesystem?
                  using (var stream = System.IO.File.Create(finalLocation))
                  {
                     await imageStream.CopyToAsync(stream);
                  }

                  //OK the quantization step
                  if (quantize > 0)
                     newView = await TryQuantize(quantize, newView, finalLocation, requester);
               }
               finally
               {
                  filelock.Release();
               }
            }
            catch
            {
               //Delete that file we added oops
               await service.DeleteAsync(newView.id, requester);
               throw;
            }

            //The view is already done.
            return newView;
         });
      }

      /// <summary>
      /// Attempt an image quantization to the given amount of colors. Physically overwrites the file. DOES NOT LOCK ON FILELOCK
      /// </summary>
      /// <param name="quantize"></param>
      /// <param name="newView"></param>
      /// <param name="fileLocation"></param>
      /// <param name="requester"></param>
      /// <returns></returns>
      protected async Task<FileView> TryQuantize(int quantize, FileView newView, string fileLocation, Requester requester)
      {
         if (newView.fileType.ToLower() != "image/png")
         {
            logger.LogWarning($"NOT quantizing non-png image {newView.id} (was {newView.fileType})!");
         }
         else if (quantize > config.MaxQuantize || quantize < config.MinQuantize)
         {
            logger.LogWarning($"Quantization colors out of range in image {newView.id} (tried: {quantize})");
         }
         else if (string.IsNullOrWhiteSpace(config.QuantizerProgram))
         {
            logger.LogWarning($"Quantization program not set! Ignoring image {newView.id} quantized to {quantize}");
         }
         else
         {
            try
            {
               var quantizeParams = $"{quantize} {fileLocation}";
               var proc = Process.Start(config.QuantizerProgram, quantizeParams);
               if (!proc.WaitForExit((int)config.QuantizeTimeout.TotalMilliseconds))
                  throw new TimeoutException($"Timed out while waiting for quantization program '{config.QuantizerProgram} {quantizeParams}'");

               //Store the requested quantization since it worked
               newView.quantization = quantize;
               newView = await service.WriteAsync(newView, requester);
            }
            catch (Exception ex)
            {
               logger.LogError($"ERROR DURING IMAGE {newView.id} QUANTIZATION TO {quantize}: {ex}");
            }
         }

         return newView;
      }

      [HttpPut("{id}")]
      [Authorize]
      public Task<ActionResult<FileView>> PutAsync([FromRoute] long id, [FromBody] FileView view)
      {
         view.id = id;
         return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
      }

      public class GetFileModify
      {
         public int size { get; set; }
         public bool crop { get; set; }
         public bool freeze { get; set; } = false;
         //public bool noGrow {get;set;}
      }

      [HttpGet("raw/{id}")]
      [ResponseCache(Duration = 13824000)] //six months
      public async Task<IActionResult> GetFileAsync([FromRoute] long id, [FromQuery] GetFileModify modify)
      {
         if (modify.size > 0)
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

         var requester = GetRequesterNoFail();

         //Go get that ONE file. This should return null if we can't read it... let's hope!
         //SPECIAL: the 0 file is 
         FileView fileData = null;

         if (id == 0)
         {
            if (System.IO.File.Exists(GetAndMakePath(0)))
               fileData = new FileView() { id = 0, fileType = "image/png" };
         }
         else
         {
            fileData = (await service.SearchAsync(new FileSearch()
            {
               Ids = new List<long>() { id },
               SearchAllBuckets = true
            }, requester)).OnlySingle();
            //FindByIdAsync(id, requester);
         }

         if (fileData == null)
            return NotFound();

         var finalPath = GetAndMakePath(fileData.id, modify);

         //Ok NOW we can go get it. We may need to perform a resize beforehand if we can't find the file.
         //NOTE: locking on the entire write may increase wait times for brand new images (and image uploads) but it saves cpu cycles
         //in those cases by only resizing an image once.
         await filelock.WaitAsync();

         try
         {
            //Will checking the fileinfo be too much??
            if (!System.IO.File.Exists(finalPath) || (new FileInfo(finalPath)).Length == 0)
            {
               var baseImage = GetPath(fileData.id);
               IImageFormat format;

               await Task.Run(() =>
               {
                  using (var image = Image.Load(baseImage, out format))
                  {
                     var maxDim = Math.Max(image.Width, image.Height);
                     var minDim = Math.Min(image.Width, image.Height);
                     var isGif = format.DefaultMimeType == "image/gif";

                     //Square ALWAYS happens, it can happen before other things.
                     if (modify.crop)
                        image.Mutate(x => x.Crop(new Rectangle((image.Width - minDim) / 2, (image.Height - minDim) / 2, minDim, minDim)));

                     //Saving as png also works, but this preserves the format (even if it's a little heavier compute, it's only a one time thing)
                     if (modify.freeze && isGif)
                     {
                        while (image.Frames.Count > 1)
                           image.Frames.RemoveFrame(1);
                     }

                     if (modify.size > 0 && !(isGif && (modify.size > image.Width || modify.size > image.Height)))
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
         }
         finally
         {
            filelock.Release();
         }

         Response.Headers.Add("ETag", GetETag(finalPath));
         return File(System.IO.File.OpenRead(finalPath), fileData.fileType);
      }

      [HttpGet]
      public Task<ActionResult<List<FileView>>> GetAsync([FromQuery] FileSearch search)
      {
         return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
      }

      [HttpGet("docs")]
      public Task<ActionResult<string>> DocsAsync()
      {
         return ThrowToAction(() => Task.FromResult(docService.GetString("doc.file", "en")));
      }
   }
}