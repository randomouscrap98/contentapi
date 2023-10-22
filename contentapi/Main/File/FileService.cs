using System.Diagnostics;
using Amazon.S3.Model;
using contentapi.Search;
using contentapi.data.Views;
using Newtonsoft.Json;
using contentapi.data;
using contentapi.Utilities;

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
    public string? DefaultImageFallback { get; set; } = null;

    public TimeSpan LoggingRetainment {get;set;} = TimeSpan.FromDays(1);

    public bool EnableUploads {get;set;} = true;
    public bool HighQualityResize {get;set;} = true;
}

public class FileService : IFileService
{
    public const string S3Prefix = "s3://";
    public const string FallbackMime = "image/png";

    protected IDbServicesFactory dbFactory;
    protected S3Provider s3Provider;
    protected ILogger logger;
    protected IImageManipulator imageManip;

    protected FileServiceConfig config;

    protected readonly SemaphoreSlim filelock = new SemaphoreSlim(1, 1);

    private static readonly List<DateTime> ImageRenders = new List<DateTime>();
    private static readonly List<DateTime> ImageLoads = new List<DateTime>();

    public FileService(ILogger<FileService> logger, IDbServicesFactory factory, FileServiceConfig config, 
        S3Provider provider, IImageManipulator imageManip) 
    {
        this.dbFactory = factory;
        this.config = config;
        this.s3Provider = provider;
        this.logger = logger;
        this.imageManip = imageManip;
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

    public object GetImageLog(TimeSpan timeframe)
    {
        return new {
            loadCount = ImageLoads.Count(x => x > DateTime.Now - timeframe),
            renderCount = ImageRenders.Count(x => x > DateTime.Now - timeframe)
        };
    }

    //Whether main files come from S3 or local, use this function
    public async Task<byte[]> GetMainDataAsync(string name)
    {
        AddImageLoad();

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
                throw new NotFoundException($"File {path} not found");

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
                return Path.GetFullPath(Path.Join(config.ThumbnailLocation, extraFolder, hash));
        }

        return "";
    }

    protected string GetAndMakeTempPath()
    {
        var result = Path.GetFullPath(Path.Join(config.TempLocation, DateTime.Now.Ticks.ToString()));
        System.IO.Directory.CreateDirectory(config.TempLocation);
        return result;
    }

    public Task<ContentView> UploadFile(UploadFileConfigExtra fileConfig, Stream fileData, long requester)
    {
        var newView = new ContentView()
        {
            name = fileConfig.name ?? "",
            contentType = InternalContentType.file,
            hash = fileConfig.hash ?? "",
            keywords = (fileConfig.keywords ?? "").Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList(),
            values = fileConfig.values.ToDictionary(x => x.Key, y => (object)y.Value)
        };

        //This may look strange: it's because we have a bit of a hack to make empty globalPerms work. We strip
        //spaces and periods, just in case the browser requires SOME character to be there for "empty"
        newView.permissions[0] = (fileConfig.globalPerms ?? "CR").Trim().Replace(".", "");

        return UploadFile(newView, fileConfig, fileData, requester);
    }

    protected void AddGeneric(List<DateTime> logging, int count)
    {
        logging.RemoveAll(x => x < DateTime.Now - config.LoggingRetainment);
        logging.Add(DateTime.Now);
    }

    public void AddImageRender(int count = 1)
    {
        lock(ImageRenders) { AddGeneric(ImageRenders, count); }
    }

    public void AddImageLoad(int count = 1)
    {
        lock(ImageLoads) { AddGeneric(ImageLoads, count); }
    }

    public async Task<ContentView> UploadFile(ContentView newView, UploadFileConfig fileConfig, Stream fileData, long requester)
    {
        if(!config.EnableUploads)
        {
            logger.LogWarning($"Uploads disabled, upload attempted by user {requester}");
            throw new ForbiddenException("Uploading not allowed at this time!");
        }

        fileData.Seek(0, SeekOrigin.Begin);

        if (fileData.Length == 0)
            throw new RequestException("No data uploaded!");

        if (fileConfig.quantize >= 0 && (fileConfig.quantize < config.MinQuantize || fileConfig.quantize > config.MaxQuantize))
            throw new RequestException($"Quantize must be between {config.MinQuantize} and {config.MaxQuantize}");

        //int trueQuantize = 0;
        var tempLocation = GetAndMakeTempPath(); //Work is done locally for quantize/etc

        var manipResult = await imageManip.FitToSizeAndSave(fileData, tempLocation, config.MaxSize);
        newView.literalType = manipResult.MimeType;
        AddImageRender(manipResult.RenderCount + manipResult.LoadCount);

        try
        {
            var meta = new Dictionary<string, object>()
            {
               { "size", manipResult.SizeInBytes },
               { "width", manipResult.Width },
               { "height", manipResult.Height }
            };

            //OK the quantization step. This SHOULD modify the view for us!
            if (fileConfig.quantize > 0)
            {
                var quantizeInfo = await TryQuantize(fileConfig.quantize, newView, tempLocation);

                if (quantizeInfo.Item1 > 0)
                {
                    meta["quantize"] = quantizeInfo.Item1;
                    meta["size"] = quantizeInfo.Item2;
                }
            }


            //We now have the metadata
            newView.meta = JsonConvert.SerializeObject(meta);

            using var tempWriter = dbFactory.CreateWriter();
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
    protected async Task<Tuple<int, long>> TryQuantize(int quantize, ContentView newView, string fileLocation) //, long requster)
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

                var fi = new FileInfo(fileLocation);

                return Tuple.Create(quantize, fi.Length);
            }
            catch (Exception ex)
            {
                logger.LogError($"ERROR DURING IMAGE {newView.id} QUANTIZATION TO {quantize}: {ex}");
            }
        }

        return Tuple.Create(-1, (long)0);
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

        if (hash == Constants.DefaultHash)
        {
            fileData = new ContentView() { id = 0, literalType = FallbackMime, hash = hash, contentType = InternalContentType.file };
        }
        else
        {
            //Doesn't matter who the requester is, ANY file with this hash is fine... what about deleted though?
            using var searcher = dbFactory.CreateSearch();
            fileData = (await searcher.GetByField<ContentView>(RequestType.content, "hash", hash)).FirstOrDefault();
        }

        if (fileData == null || fileData.deleted || fileData.contentType != InternalContentType.file)
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
            if (hash == Constants.DefaultHash && !string.IsNullOrWhiteSpace(config.DefaultImageFallback))
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
        //NOTE: locking on the entire write may increase wait times for brand new images (and image uploads) but it saves cpu cycles
        //in those cases by only resizing an image once.
        await filelock.WaitAsync();

        try
        {
            //Will checking the fileinfo be too much??
            if (!System.IO.File.Exists(thumbnailPath) || (new FileInfo(thumbnailPath)).Length == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath) ?? throw new InvalidOperationException("No parent for thumbail path?"));

                using(var memStream = new MemoryStream(await GetMainDataAsync(hash)))
                {
                    var manipResult = await imageManip.MakeThumbnailAndSave(memStream, thumbnailPath, modify);
                    AddImageRender(manipResult.RenderCount + manipResult.LoadCount);
                }
            }
        }
        finally
        {
            filelock.Release();
        }

        return await System.IO.File.ReadAllBytesAsync(thumbnailPath);
    }
}