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

    public async Task GeneralIMagickResize(string filename, string resize, bool crop, bool freeze, string outfile, ImageManipulationInfo baseInfo)
    {
        //NOTE: LOOKUP HOW TO CROP IN IMAGICK, IT MIGHT CHANGE HOW THIS FUNCTION OPERATES!

        //Always lookup the initial info first, need the mimetype and maybe other things
        await FillImageManipulationInfo(filename, baseInfo);

        //Now that we know the mimetype, maybe we can do special things. gif requires a different kind of resize, and
        //additional flags

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
            var resizeFactor = config.InitialResize;

            while(result.SizeInBytes > maxSize)
            {
                if(result.RenderCount > config.MaxResizes)
                    throw new RequestException("Tried to resize too many times, couldn't ");

                result.RenderCount++;
                resizeFactor = resizeFactor - config.ResizeStep;
            }
            //Spawn imagick process for fitting the file to the given size. This may still require imagesharp to 
            //get information about the file! Unless this is something imagick can report...
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
            //Spawn imagick process for producing the needed file. This may still require imagesharp to 
            //get information about the file!
        });

        return result;
    }
}