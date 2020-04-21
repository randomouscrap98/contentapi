using System.IO;

namespace contentapi.Services
{
    public interface IFileUploadHandler
    {
        Stream GetFile(string name, string identifier, long squareLimit);
        void PutFile(Stream file, string identifier);
    }
}