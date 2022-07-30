using contentapi.data;

namespace contentapi.Utilities;

public class ImageManipulator_IMagickConfig
{
    public string IMagickPath {get;set;} = "";
    public string TempPath {get;set;} = "";
}

public class ImageManipulator_IMagick : IImageManipulator
{
    protected ILogger logger;
    protected ImageManipulator_IMagickConfig config;

    //Put the commands as constants maybe idk

    public ImageManipulator_IMagick(ILogger<ImageManipulator_IMagick> logger, ImageManipulator_IMagickConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    /// <summary>
    /// Run the given function by creating a file from the filestream, passing the path to the runnable, then
    /// removing the temp file afterwards
    /// </summary>
    /// <param name="fileData"></param>
    /// <param name="runnable"></param>
    /// <returns></returns>
    public async Task RunWithFile(Stream fileData, Func<string, Task> runnable) 
    {
        var tempFile = Path.GetFullPath(Path.Combine(config.TempPath, Guid.NewGuid().ToString().Replace("-", "")));
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile) ?? throw new InvalidOperationException("No temp file path!"));

        using(var file = File.Create(tempFile))
        {
            fileData.Seek(0, SeekOrigin.Begin);
            await fileData.CopyToAsync(file);
        }

        try
        {
            await runnable(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    public async Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize)
    {
        var result = new ImageManipulationInfo();

        await RunWithFile(fileData, async (path) =>
        {
            //Spawn imagick process for fitting the file to the given size. This may still require imagesharp to 
            //get information about the file! Unless this is something imagick can report...
        });

        return result;
    }

    public async Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify)
    {
        var result = new ImageManipulationInfo();

        await RunWithFile(fileData, async (path) =>
        {
            //Spawn imagick process for producing the needed file. This may still require imagesharp to 
            //get information about the file!
        });

        return result;
    }
}