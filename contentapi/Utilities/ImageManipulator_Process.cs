using contentapi.data;

namespace contentapi.Utilities;

public class ImageManipulator_Process : IImageManipulator
{
    protected ILogger logger;

    public ImageManipulator_Process(ILogger<ImageManipulator_Process> logger)
    {
        this.logger = logger;
    }

    public Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize)
    {
        //- First, save stream to file
        //- Then, spawn self as new process with special parameters
        //- The output is JSON of the return value
        throw new NotImplementedException();
    }

    public Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify)
    {
        throw new NotImplementedException();
    }
}