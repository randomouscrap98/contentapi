using contentapi.data.Views;

namespace contentapi.Main;

public interface IFileService
{
    Task<ContentView> UploadFile(ContentView view, UploadFileConfig fileConfig, Stream fileData, long requester);
    Task<ContentView> UploadFile(UploadFileConfigExtra fileConfig, Stream fileData, long requester);
    Task<Tuple<byte[], string>> GetFileAsync(string hash, GetFileModify modify);

    object GetImageLog(TimeSpan timeframe);
}