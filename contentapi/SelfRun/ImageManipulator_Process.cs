using contentapi.data;
using contentapi.Utilities;

namespace contentapi.SelfRun;

public class ImageManipulationFitToSizeArgument : IInfileArgument
{
    public string inFile { get;set; } = "";
    public string savePath {get;set;} = "";
    public int maxSize {get;set;}
}

public class ImageManipulationMakeThumbnailArgument : IInfileArgument
{
    public string inFile { get;set; } = "";
    public string savePath {get;set;} = "";
    public GetFileModify modify {get;set;} = new GetFileModify();
}

/// <summary>
/// An image manipulator that runs the modifications through a separate process rather than directly through
/// library calls.
/// </summary>
public class ImageManipulator_Process : IImageManipulator
{
    public Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize)
    {
        return SelfRunSystem.RunProcessWithFileAsync<ImageManipulationInfo>(fileData, SelfRunSystem.RunImageResize, new ImageManipulationFitToSizeArgument
        {
            maxSize = maxSize,
            savePath = savePath
        });
    }

    public Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify)
    {
        return SelfRunSystem.RunProcessWithFileAsync<ImageManipulationInfo>(fileData, SelfRunSystem.RunImageThumbnail, new ImageManipulationMakeThumbnailArgument
        {
            modify = modify,
            savePath = savePath
        });
    }

    public static async Task<object> SelfRunCall(string runType, string argument, ILoggerFactory factory)
    {
        var logger = factory.CreateLogger<ImageManipulator_Direct>();
        var manipulator = new ImageManipulator_Direct(logger);

        if(runType == SelfRunSystem.RunImageResize)
        {
            var realArg = SelfRunSystem.ParseArgument<ImageManipulationFitToSizeArgument>(argument);
            using(var file = File.OpenRead(realArg.inFile))
            {
                return await manipulator.FitToSizeAndSave(file, realArg.savePath, realArg.maxSize);
            }
        }
        else if(runType == SelfRunSystem.RunImageThumbnail)
        {
            var realArg = SelfRunSystem.ParseArgument<ImageManipulationMakeThumbnailArgument>(argument);
            using(var file = File.OpenRead(realArg.inFile))
            {
                return await manipulator.MakeThumbnailAndSave(file, realArg.savePath, realArg.modify);
            }
        }
        else
        {
            throw new InvalidOperationException($"Unknown runtype for image manipulator: {runType}");
        }
    }
}