using contentapi.data;

namespace contentapi.Utilities;

public class ImageManipulationInfo
{
    public string MimeType {get;set;} = "";
    public int RenderCount {get;set;}
    public int LoadCount {get;set;}
    public int Width {get;set;}
    public int Height {get;set;}
    public long SizeInBytes {get;set;}
}

public interface IImageManipulator
{
    Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize);
    Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify);
}