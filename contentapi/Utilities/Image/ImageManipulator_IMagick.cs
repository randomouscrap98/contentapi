using System.Diagnostics;
using contentapi.data;
using Newtonsoft.Json;

namespace contentapi.Utilities;

public class ImageManipulator_IMagickConfig
{
    public string IMagickPath {get;set;} = "";
    public string TempPath {get;set;} = "";
    public double InitialResize {get;set;} = 0.7;
    public double ResizeStep {get;set;} = 0.15;
    public double MaxResizes {get;set;} = 4;
}

public class ImageManipulator_IMagick : IImageManipulator
{
    public const int MaximumConcurrentManipulations = 1;

    protected ILogger logger;
    protected ImageManipulator_IMagickConfig config;
    
    protected static SemaphoreSlim ManipLock = new SemaphoreSlim(MaximumConcurrentManipulations, MaximumConcurrentManipulations);

    //Put the commands as constants maybe idk

    public ImageManipulator_IMagick(ILogger<ImageManipulator_IMagick> logger, ImageManipulator_IMagickConfig config)
    {
        this.logger = logger;
        this.config = config;

        if(config.MaxResizes * config.ResizeStep >= config.InitialResize)
            throw new InvalidOperationException("Configuration error: ResizeStep * MaxResizes >= InitialResize! Image shrink goes to negative!");
    }

    /// <summary>
    /// Run imagick with the given commands. Automatically throws exceptions on bad exit codes, and consumes
    /// the output for you.
    /// </summary>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public async Task<string> RunImagick(List<string> arguments)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = config.IMagickPath,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = config.TempPath,
        };

        foreach(var arg in arguments)  
            startInfo.ArgumentList.Add(arg);
        
        await ManipLock.WaitAsync();

        try
        {
            logger.LogDebug($"Starting imagick process {startInfo.FileName} {startInfo.Arguments}");
            var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Can't spawn process {startInfo.FileName} with arguments {string.Join(",", startInfo.ArgumentList)}!");
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if(process.ExitCode != 0)
                throw new InvalidOperationException($"Process {startInfo.FileName} exited with code {process.ExitCode}, output: {output}");
            else
                return output;
        }
        finally
        {
            ManipLock.Release();
        }
    }

    /// <summary>
    /// Fill or otherwise create the image manipulation info for the given filename. Note that this spawns an imagick process!
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public async Task<ImageManipulationInfo> FillImageManipulationInfo(string filename, ImageManipulationInfo? info = null)
    {
        var raw = await RunImagick(new List<string> { filename + "[0]", "json:"});
        var result = ParseImageManipulationInfo(raw, info);
        result.LoadCount++;
        return result;
    }

    /// <summary>
    /// Perform the ridiculous parsing required to convert from imagick json output to ImageManipulationInfo
    /// </summary>
    /// <param name="rawImagickOutput"></param>
    /// <param name="info"></param>
    /// <returns></returns>
    public ImageManipulationInfo ParseImageManipulationInfo(string rawImagickOutput, ImageManipulationInfo? info = null)
    {
        var parsed = JsonConvert.DeserializeObject<List<IMagickJson>>(rawImagickOutput) ?? throw new InvalidOperationException("Couldn't parse output of imagick info!");
        var jsonInfo = parsed.FirstOrDefault() ?? throw new InvalidOperationException("Couldn't find any json objects within the output array!");
        var imageInfo = jsonInfo.image ?? throw new InvalidOperationException("No 'image' info parsed out of the json!");
        var geometryInfo = imageInfo.geometry ?? throw new InvalidOperationException("No 'geometry' info parsed out of image json!");

        var realInfo = info ?? new ImageManipulationInfo();

        realInfo.Width = geometryInfo.width;
        realInfo.Height = geometryInfo.height;
        realInfo.MimeType = imageInfo.mimeType ?? throw new InvalidOperationException("No 'mimeType' found in image json!");

        return realInfo;
    }

    /// <summary>
    /// Perform a resize of the file at the given location, with the given resize arg, saving to the given outfile.
    /// Note that "baseInfo" MUST be pre-filled with image information for this to function properly!
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="resize"></param>
    /// <param name="freeze"></param>
    /// <param name="outfile"></param>
    /// <param name="baseInfo"></param>
    /// <returns></returns>
    public async Task GeneralIMagickResize(string filename, ImageManipulationInfo baseInfo, GetFileModify modify, string outfile) //string resize, bool freeze, string outfile)
    {
        if(string.IsNullOrWhiteSpace(baseInfo.MimeType))
            throw new InvalidOperationException("baseInfo must contain the image mimeType!");

        var arglist = new List<string>();

        //Cropping can be done with the resize arg, add ^ to the end
        var resize = $"{modify.size}x{modify.size}";

        if(modify.crop)
            resize += "^";
        
        if(baseInfo.MimeType == Constants.GifMime)
            resize += ">"; //Only size downwards
        
        arglist.Add(resize);

        if(modify.crop)
            arglist.AddRange(new [] { "-gravity", "center", "-extent", $"{modify.size}x{modify.size}"});
        
        arglist.AddRange(new[] { "-write", outfile, "json:" });
        
        ParseImageManipulationInfo(await RunImagick(arglist), baseInfo);

        baseInfo.LoadCount++;
        baseInfo.RenderCount++;
        baseInfo.SizeInBytes = new FileInfo(outfile).Length;
    }

    public async Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize)
    {
        var result = new ImageManipulationInfo()
        {
            RenderCount = 0,
            LoadCount = 0,
            SizeInBytes = fileData.Length
        };

        logger.LogTrace($"FitToSize called with size {maxSize}, image bytes {result.SizeInBytes}");

        await fileData.TemporaryFileTask(config.TempPath, async (path) =>
        {
            await FillImageManipulationInfo(path, result);
            var resizeFactor = config.InitialResize;
            var resizeBasis = Math.Max(result.Width, result.Height);
            var modify = new GetFileModify()
            {
                crop = false,
                freeze = false
            };

            while(result.SizeInBytes > maxSize)
            {
                if(result.RenderCount > config.MaxResizes)
                    throw new RequestException("Tried to resize too many times, couldn't ");

                modify.size = (int)(resizeBasis * resizeFactor);

                //Resize here
                await GeneralIMagickResize(path, result, modify, savePath);

                resizeFactor = resizeFactor - config.ResizeStep;
            }
        });

        return result;
    }

    public async Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify)
    {
        var result = new ImageManipulationInfo()
        {
            RenderCount = 0,
            LoadCount = 0,
            SizeInBytes = fileData.Length
        };

        await fileData.TemporaryFileTask(config.TempPath, async (path) =>
        {
            await FillImageManipulationInfo(path, result);
            await GeneralIMagickResize(path, result, modify, savePath);
        });

        return result;
    }
}