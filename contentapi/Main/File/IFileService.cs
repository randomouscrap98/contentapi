using contentapi.Views;

namespace contentapi.Main;

public interface IFileService
{
    Task<ContentView> UploadFile(UploadFileConfig fileConfig, Stream fileData, long requester);
    Task<Tuple<byte[], string>> GetFileAsync(string hash, GetFileModify modify);
}