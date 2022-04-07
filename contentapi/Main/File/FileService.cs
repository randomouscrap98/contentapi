using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace contentapi.Main;

public class FileServiceConfig
{
    public string MainLocation { get; set; } = "uploads"; //Can also be an S3 url
    public string ThumbnailLocation { get; set; } = "uploads"; // Can ONLY be local!
    public string TempLocation { get; set; } = "filecontroller_temp"; //Can ONLY be local!
    public int MaxSize { get; set; } = 2000000;
    public string? QuantizerProgram { get; set; } = null; //Set this IF you have a quantizer!
    public TimeSpan QuantizeTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public int MaxQuantize { get; set; } = 256;
    public int MinQuantize { get; set; } = 2;
    public double ResizeRepeatFactor { get; set; } = 0.8;
    public string DefaultHash { get; set; } = "0";
    public string? DefaultImageFallback { get; set; } = null;
}

public class FileService : IFileService
{
    public const string S3Prefix = "s3://";
    public const string GifMime = "image/gif";
    public const string FallbackMime = "image/png";

    protected Func<IDbWriter> writeProvider;
    protected Func<IGenericSearch> searchProvider;
    protected S3Provider s3Provider;
    protected ILogger logger;

    protected FileServiceConfig config;

    protected readonly SemaphoreSlim filelock = new SemaphoreSlim(1, 1);

    public FileService(ILogger<FileService> logger, Func<IDbWriter> writer, Func<IGenericSearch> searcher, FileServiceConfig config, S3Provider provider) //IAmazonS3 s3client)
    {
        this.writeProvider = writer;
        this.searchProvider = searcher;
        this.config = config;
        //this.s3client = s3client;
        this.s3Provider = provider;
        this.logger = logger;
    }

    public string GetBucketName()
    {
        return config.MainLocation.Replace(S3Prefix, "");
    }

    public async Task<byte[]> GetS3DataAsync(string name)
    {
        var obj = await s3Provider.GetCachedProvider().GetObjectAsync(GetBucketName(), name);
        using (var stream = new MemoryStream())
        {
            await obj.ResponseStream.CopyToAsync(stream);
            return stream.ToArray();
        }
    }

    public Task PutS3DataAsync(Stream data, string name, string mimeType)
    {
        //Oh, everything was ok! Now let's upload while we still have the lock
        var putRequest = new PutObjectRequest
        {
            Key = name,
            BucketName = GetBucketName(),
            InputStream = data,
            AutoCloseStream = true,
            ContentType = mimeType
        };

        return s3Provider.GetCachedProvider().PutObjectAsync(putRequest);
    }

    public bool IsS3()
    {
        return config.MainLocation.StartsWith(S3Prefix);
    }

    //Whether main files come from S3 or local, use this function
    public async Task<byte[]> GetMainDataAsync(string name)
    {
        if (IsS3())
        {
            try
            {
                return await GetS3DataAsync(name);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Exception from S3 get for {name}: {ex}");
                throw new NotFoundException($"File {name} not found", ex);
            }
        }
        else
        {
            var path = Path.Join(config.MainLocation, name);

            if (!File.Exists(path))
                throw new NotFoundException($"File {name} not found");

            return await File.ReadAllBytesAsync(path);
        }
    }

    public string GetAndMakeMainPath(string name)
    {
        Directory.CreateDirectory(config.MainLocation);
        return Path.Join(config.MainLocation, name);
    }

    public async Task SaveMainDataAsync(string tempFilePath, string name, string mimeType)
    {
        if (IsS3())
        {
            await PutS3DataAsync(System.IO.File.OpenRead(tempFilePath), name, mimeType);
        }
        else
        {
            System.IO.File.Copy(tempFilePath, GetAndMakeMainPath(name));
        }
    }

    public async Task SaveMainDataAsync(byte[] rawData, string name, string mimeType)
    {
        if (IsS3())
        {
            using (var memStream = new MemoryStream(rawData))
                await PutS3DataAsync(memStream, name, mimeType);
        }
        else
        {
            await System.IO.File.WriteAllBytesAsync(GetAndMakeMainPath(name), rawData);
        }
    }

    public string GetThumbnailPath(string hash, GetFileModify? modify = null)
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
                return Path.Join(config.ThumbnailLocation, extraFolder, hash);
        }

        return "";
    }

    protected string GetAndMakeTempPath()
    {
        var result = Path.Join(config.TempLocation, DateTime.Now.Ticks.ToString());
        System.IO.Directory.CreateDirectory(config.TempLocation);
        return result;
    }

    public async Task<ContentView> UploadFile(UploadFileConfig fileConfig, Stream fileData, long requester) //[FromForm] UploadFileModel model)
    {
        fileData.Seek(0, SeekOrigin.Begin);

        if (fileData.Length == 0)
            throw new RequestException("No data uploaded!");

        if (fileConfig.quantize >= 0 && (fileConfig.quantize < config.MinQuantize || fileConfig.quantize > config.MaxQuantize))
            throw new RequestException($"Quantize must be between {config.MinQuantize} and {config.MaxQuantize}");

        var newView = new ContentView()
        {
            name = fileConfig.name ?? "",
            contentType = Db.InternalContentType.file,
            values = fileConfig.values.ToDictionary(x => x.Key, y => (object)y.Value)
        };

        //This may look strange: it's because we have a bit of a hack to make empty globalPerms work. We strip
        //spaces and periods, just in case the browser requires SOME character to be there for "empty"
        newView.permissions[0] = (fileConfig.globalPerms ?? "CR").Trim().Replace(".", "");

        IImageFormat? format = null;
        long imageByteCount = fileData.Length;
        int width = 0;
        int height = 0;
        int trueQuantize = 0;
        var tempLocation = GetAndMakeTempPath(); //Work is done locally for quantize/etc

        //The memory stream is our temporary working space for things like resize, etc. I don't want to use
        //the stream they gave us
        using (var memStream = new MemoryStream())
        {
            await fileData.CopyToAsync(memStream);
            fileData.Seek(0, SeekOrigin.Begin);

            //This will throw an exception if it's not an image (most likely)
            using (var image = Image.Load(fileData, out format))
            {
                newView.literalType = format.DefaultMimeType;

                double sizeFactor = config.ResizeRepeatFactor;
                width = image.Width;
                height = image.Height;

                while (fileConfig.tryResize && imageByteCount > config.MaxSize && sizeFactor > 0)
                {
                    double resize = 1 / Math.Sqrt(imageByteCount / (config.MaxSize * sizeFactor));
                    logger.LogWarning($"User image too large ({imageByteCount}), trying ONE resize by {resize}");
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

            logger.LogDebug($"New image size: {imageByteCount}");
            memStream.Seek(0, SeekOrigin.Begin);

            using (var stream = System.IO.File.Create(tempLocation))
            {
                await memStream.CopyToAsync(stream);
            }
        }

        try
        {
            var meta = new Dictionary<string, object>()
            {
               { "size", imageByteCount },
               { "width", width },
               { "height", height }
            };

            //OK the quantization step. This SHOULD modify the view for us!
            if (fileConfig.quantize > 0)
                trueQuantize = await TryQuantize(fileConfig.quantize, newView, tempLocation);

            if (trueQuantize > 0)
                meta["quantize"] = trueQuantize;

            //We now have the metadata
            newView.meta = JsonConvert.SerializeObject(meta);

            var tempWriter = writeProvider();
            //This is QUITE dangerous: a file could be created in the api first and THEN the file write fails!
            newView = await tempWriter.WriteAsync(newView, requester);

            try
            {
                await SaveMainDataAsync(tempLocation, newView.hash, newView.literalType ?? "");
            }
            catch (Exception ex)
            {
                logger.LogError($"FILE WRITE '{newView.hash}'({newView.id}) FAILED: {ex}");
                await tempWriter.DeleteAsync<ContentView>(newView.id, requester);
            }
        }
        finally
        {
            System.IO.File.Delete(tempLocation);
        }

        //The view is already done.
        return newView;
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
            logger.LogWarning($"NOT quantizing non-png image {newView.id} (was {newView.literalType})!");
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
        if (CheckQuantize(newView, quantize))
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

                logger.LogInformation($"Quantized {fileLocation} to {quantize} colors using '{config.QuantizerProgram} {quantizeParams}'");

                return quantize;
            }
            catch (Exception ex)
            {
                logger.LogError($"ERROR DURING IMAGE {newView.id} QUANTIZATION TO {quantize}: {ex}");
            }
        }

        return -1;
    }

    public async Task<Tuple<byte[], string>> GetFileAsync(string hash, GetFileModify modify)
    {
        if (modify.size > 0)
        {
            if (modify.size < 10)
                throw new RequestException("Requested file size too small!");
            else if (modify.size <= 100)
                modify.size = 10 * (modify.size / 10);
            else if (modify.size <= 1000)
                modify.size = 100 * (modify.size / 100);
            else
                throw new RequestException("Requested file size too large!");
        }

        //Go get that ONE file. This should return null if we can't read it... let's hope!
        //SPECIAL: the 0 file is 
        ContentView? fileData = null;

        if (hash == config.DefaultHash)
        {
            fileData = new ContentView() { id = 0, literalType = FallbackMime, hash = hash, contentType = Db.InternalContentType.file };
        }
        else
        {
            //Doesn't matter who the requester is, ANY file with this hash is fine... what about deleted though?
            fileData = (await searchProvider().GetByField<ContentView>(RequestType.content, "hash", hash)).FirstOrDefault();
        }

        if (fileData == null || fileData.deleted || fileData.contentType != Db.InternalContentType.file)
            throw new NotFoundException($"Couldn't find file data with hash {hash}");

        var thumbnailPath = GetThumbnailPath(hash, modify);
        var mimeType = fileData.literalType ?? "";

        try
        {
            //This means they're requesting a MAIN data, just go get it 
            if (string.IsNullOrEmpty(thumbnailPath))
            {
                return Tuple.Create(await GetMainDataAsync(hash), mimeType);
            }
            else
            {
                return Tuple.Create(await GetThumbnailAsync(hash, thumbnailPath, modify), mimeType);
            }
        }
        catch (NotFoundException)
        {
            //This just means we CAN generate it!
            if (hash == config.DefaultHash && !string.IsNullOrWhiteSpace(config.DefaultImageFallback))
            {
                logger.LogInformation($"Creating default image {hash} from base64 string given in config");
                await SaveMainDataAsync(Convert.FromBase64String(config.DefaultImageFallback), hash, mimeType);
                return await GetFileAsync(hash, modify);
            }
            else
            {
                throw;
            }
        }
    }

    public async Task<byte[]> GetThumbnailAsync(string hash, string thumbnailPath, GetFileModify modify)
    {
        //Ok NOW we can go get it. We may need to perform a resize beforehand if we can't find the file.
        //NOTE: locking on the entire write may increase wait times for brand new images (and image uploads) but it saves cpu cycles
        //in those cases by only resizing an image once.
        await filelock.WaitAsync();

        try
        {
            //Will checking the fileinfo be too much??
            if (!System.IO.File.Exists(thumbnailPath) || (new FileInfo(thumbnailPath)).Length == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath) ?? throw new InvalidOperationException("No parent for thumbail path?"));
                IImageFormat format;

                var baseData = await GetMainDataAsync(hash);

                await Task.Run(() =>
                {
                    using (var image = Image.Load(baseData, out format))
                    {
                        var maxDim = Math.Max(image.Width, image.Height);
                        var minDim = Math.Min(image.Width, image.Height);
                        var isGif = format.DefaultMimeType == GifMime;

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

                        using (var stream = System.IO.File.OpenWrite(thumbnailPath))
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

        return await System.IO.File.ReadAllBytesAsync(thumbnailPath);
    }
}