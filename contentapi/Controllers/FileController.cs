using System.Diagnostics;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace contentapi.Controllers;

public class FileControllerConfig
{
   public string Location { get; set; } = "uploads";
   public string TempLocation {get;set;} = "filecontroller_temp";
   public int MaxSize { get; set; } = 2000000;
   public string? QuantizerProgram { get; set; } = null; //Set this IF you have a quantizer!
   public TimeSpan QuantizeTimeout { get; set; } = TimeSpan.FromSeconds(20);
   public int MaxQuantize { get; set; } = 256;
   public int MinQuantize { get; set; } = 2;
   public double ResizeRepeatFactor {get;set;} = 0.8;
   public string DefaultHash {get;set;} = "0";
   public string? DefaultImageFallback {get;set;} = null;
}

public class FileController : BaseController
{
   protected FileControllerConfig config;

   protected static readonly SemaphoreSlim filelock = new SemaphoreSlim(1, 1);

   public FileController(BaseControllerServices services, FileControllerConfig config)
         : base(services)
   {
      this.config = config;
   }

   protected string GetUploadPath(string hash, GetFileModify? modify = null)
   {
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
            return Path.Join(config.Location, extraFolder, hash);
      }

      return Path.Join(config.Location, hash);
   }

   //Convert path to appropriate etag
   protected string GetETag(string path)
   {
      return path.Replace(config.Location, "").Replace("\\", "Z").Replace("/", "Z");
   }

   //Should be thread safe
   protected string GetAndMakeUploadPath(string hash, GetFileModify? modify = null)
   {
      var result = GetUploadPath(hash, modify);
      System.IO.Directory.CreateDirectory(Path.GetDirectoryName(result) ?? throw new InvalidOperationException("Couldn't compute path for file upload!"));
      return result;
   }

   protected string GetAndMakeTempPath()
   {
      var result = Path.Join(config.TempLocation, DateTime.Now.Ticks.ToString());
      System.IO.Directory.CreateDirectory(config.TempLocation);
      return result;
   }

   public class UploadFileModel
   {
      public IFormFile? file {get;set;} = null;
      public List<IFormFile>? files = null;
      public string? name {get;set;}= null;
      public bool tryResize {get;set;} = true;
      public int quantize {get;set;} = -1; 
      public string? globalPerms {get;set;} = null;
      public Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();
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

      if (model.file.Length == 0)
         return BadRequest("No data uploaded!");
      
      if(model.quantize >= 0 && (model.quantize < config.MinQuantize || model.quantize > config.MaxQuantize))
         return BadRequest($"Quantize must be between {config.MinQuantize} and {config.MaxQuantize}");
      
      return await MatchExceptions(async () =>
      {
         RateLimit(RateFile);

         var requester = GetUserIdStrict();

         var newView = new ContentView() { 
            name = model.name ?? "",
            contentType = Db.InternalContentType.file,
            values = model.values.ToDictionary(x => x.Key, y => (object)y.Value)
         };

         //This may look strange: it's because we have a bit of a hack to make empty globalPerms work. We strip
         //spaces and periods, just in case the browser requires SOME character to be there for "empty"
         newView.permissions[0] = (model.globalPerms ?? "CR").Trim().Replace(".", "");

         IImageFormat? format = null;
         long imageByteCount = model.file.Length;
         int width = 0; 
         int height = 0;
         int trueQuantize = 0;
         Stream imageStream = model.file.OpenReadStream();
         var tempLocation = GetAndMakeTempPath();

         using(var memStream = new MemoryStream())
         {
            await imageStream.CopyToAsync(memStream);
            imageStream.Seek(0, SeekOrigin.Begin);

            //This will throw an exception if it's not an image (most likely)
            using (var image = Image.Load(imageStream, out format))
            {
               newView.literalType = format.DefaultMimeType;

               double sizeFactor = config.ResizeRepeatFactor;
               width = image.Width;
               height = image.Height;

               while (model.tryResize && imageByteCount > config.MaxSize && sizeFactor > 0)
               {
                  double resize = 1 / Math.Sqrt(imageByteCount / (config.MaxSize * sizeFactor));
                  services.logger.LogWarning($"User image too large ({imageByteCount}), trying ONE resize by {resize}");
                  width = (int)(width * resize);
                  height = (int)(height * resize);
                  image.Mutate(x => x.Resize(width, height, KnownResamplers.Lanczos3));

                  memStream.SetLength(0);
                  image.Save(memStream, format);
                  imageByteCount = memStream.Length;

                  //Keep targeting an EVEN more harsh error margin (even if it's incorrect because
                  //it stacks with previous resizes), also this makes the loop guaranteed to end
                  sizeFactor -= 0.2;
               }

               //If the image is still too big, bad ok bye
               if (imageByteCount > config.MaxSize)
                  throw new RequestException("File too large!");
                  
            }

            services.logger.LogDebug($"New image size: {imageByteCount}");
            memStream.Seek(0, SeekOrigin.Begin);

            using (var stream = System.IO.File.Create(tempLocation))
            {
               await memStream.CopyToAsync(stream);
            }
         }

         try
         {
            var meta = new Dictionary<string, object> ()
            {
               { "size", imageByteCount },
               { "width", width },
               { "height", height }
            };

            //OK the quantization step. This SHOULD modify the view for us!
            if (model.quantize > 0)
               trueQuantize = await TryQuantize(model.quantize, newView, tempLocation);
            
            if(trueQuantize > 0)
               meta["quantize"] = trueQuantize;

            //We now have the metadata
            newView.meta = JsonConvert.SerializeObject(meta);

            //This is QUITE dangerous: a file could be created in the api first and THEN the file write fails!
            newView = await services.writer.WriteAsync(newView, requester);
            string finalLocation = "";

            try
            {
               finalLocation = GetAndMakeUploadPath(newView.hash);
               System.IO.File.Copy(tempLocation, finalLocation);
            }
            catch(Exception ex)
            {
               services.logger.LogError($"FILE WRITE '{finalLocation}' FAILED: {ex}");
               await services.writer.DeleteAsync<ContentView>(newView.id, requester);
            }
         }
         finally
         {
            System.IO.File.Delete(tempLocation);
         }

         //The view is already done.
         return newView;
      });
   }

   /// <summary>
   /// Returns whether or not the quantization is acceptable.
   /// </summary>
   /// <param name="newView"></param>
   /// <param name="quantize"></param>
   /// <returns></returns>
   protected bool CheckQuantize(ContentView newView, int quantize)
   {
      if (newView.literalType?.ToLower() != "image/png")
      {
         services.logger.LogWarning($"NOT quantizing non-png image {newView.id} (was {newView.literalType})!");
      }
      else if (quantize > config.MaxQuantize || quantize < config.MinQuantize)
      {
         services.logger.LogWarning($"Quantization colors out of range in image {newView.id} (tried: {quantize})");
      }
      else if (string.IsNullOrWhiteSpace(config.QuantizerProgram))
      {
         services.logger.LogWarning($"Quantization program not set! Ignoring image {newView.id} quantized to {quantize}");
      }
      else
      {
         return true;
      }

      return false;
   }

   /// <summary>
   /// Attempt an image quantization to the given amount of colors. Physically overwrites the file. DOES NOT LOCK ON FILELOCK
   /// </summary>
   /// <param name="quantize"></param>
   /// <param name="newView"></param>
   /// <param name="fileLocation"></param>
   /// <param name="requester"></param>
   /// <returns></returns>
   protected async Task<int> TryQuantize(int quantize, ContentView newView, string fileLocation) //, long requster)
   {
      if(CheckQuantize(newView, quantize))
      {
         try
         {
            var tempLocation = fileLocation + "_quantized";
            var quantizeParams = $"{quantize} {fileLocation} --output {tempLocation}";
            var command = $"{config.QuantizerProgram} {quantizeParams}";
            var proc = Process.Start(config.QuantizerProgram ?? throw new InvalidOperationException("Program null after null check??"), quantizeParams);

            //No use setting up multiple await/async etc
            var completed = await Task.Run(() => proc.WaitForExit((int)config.QuantizeTimeout.TotalMilliseconds));

            if (!completed)
               throw new TimeoutException($"Timed out while waiting for quantization program '{command}'");
            if (proc.ExitCode != 0)
               throw new InvalidOperationException($"Quantize program '{command}' exited with code {proc.ExitCode}");

            System.IO.File.Delete(fileLocation);               //Delete the old file
            System.IO.File.Move(tempLocation, fileLocation);   //Rename the new file

            services.logger.LogInformation($"Quantized {fileLocation} to {quantize} colors using '{config.QuantizerProgram} {quantizeParams}'");

            return quantize; 
         }
         catch (Exception ex)
         {
            services.logger.LogError($"ERROR DURING IMAGE {newView.id} QUANTIZATION TO {quantize}: {ex}");
         }
      }

      return -1;
   }

   public class GetFileModify
   {
      public int size { get; set; }
      public bool crop { get; set; }
      public bool freeze { get; set; } = false;
   }

   [HttpGet("raw/{hash}")]
   [ResponseCache(Duration = 13824000)] //six months
   public async Task<IActionResult> GetFileAsync([FromRoute] string hash, [FromQuery] GetFileModify modify)
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

      var requester = GetUserId(); //GetRequesterNoFail();

      //Go get that ONE file. This should return null if we can't read it... let's hope!
      //SPECIAL: the 0 file is 
      ContentView? fileData = null;

      if (hash == config.DefaultHash)
      {
         var defaultFile = GetAndMakeUploadPath(hash); //The baseline file, no modifications
         bool exists = false;

         //Make sure we're not hitting the filesystem too often; only check for existence once, even though
         //we may be writing the file using some default fallback
         if(System.IO.File.Exists(defaultFile))
         {
            exists = true;
         }
         else if (!string.IsNullOrWhiteSpace(config.DefaultImageFallback))
         {
            services.logger.LogInformation($"Creating default image {hash} from base64 string given in config");
            await System.IO.File.WriteAllBytesAsync(defaultFile, Convert.FromBase64String(config.DefaultImageFallback)); //System.Text.Encoding);
            exists = true;
         }
            
         if(exists)
            fileData = new ContentView() { id = 0, literalType = "image/png", hash = hash, contentType = Db.InternalContentType.file };
      }
      else
      {
         //Doesn't matter who the requester is, ANY file with this hash is fine... what about deleted though?
         fileData = (await services.searcher.GetByField<ContentView>(RequestType.content, "hash", hash)).FirstOrDefault();
      }

      if (fileData == null || fileData.deleted || fileData.contentType != Db.InternalContentType.file)
         return NotFound();

      var finalPath = GetAndMakeUploadPath(fileData.hash, modify);

      //Ok NOW we can go get it. We may need to perform a resize beforehand if we can't find the file.
      //NOTE: locking on the entire write may increase wait times for brand new images (and image uploads) but it saves cpu cycles
      //in those cases by only resizing an image once.
      await filelock.WaitAsync();

      try
      {
         //Will checking the fileinfo be too much??
         if (!System.IO.File.Exists(finalPath) || (new FileInfo(finalPath)).Length == 0)
         {
            var baseImage = GetUploadPath(fileData.hash);
            IImageFormat format;

            if(!System.IO.File.Exists(baseImage))
               throw new InvalidOperationException($"Somehow, filedata exists for {hash}({fileData.id}) but the base file was not found!");

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
      return File(System.IO.File.OpenRead(finalPath), fileData.literalType ?? throw new InvalidOperationException("Retrieved API file data somehow did not have a set literalType (mimetype for file)!"));
   }
}