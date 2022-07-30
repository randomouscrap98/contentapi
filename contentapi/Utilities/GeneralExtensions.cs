using System.Collections;
using System.Net.WebSockets;
using contentapi.data;
using Newtonsoft.Json;

namespace contentapi.Utilities;

public static class GeneralExtensions
{
    public const int ReceiveObjectAsyncBufferSize = 4096;

    /// <summary>
    /// Returns whether or not the given type is the given generic type (probably some kind of container)
    /// </summary>
    /// <param name="type"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool IsGenericType(this Type type, Type genericType)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableTo(genericType);
    }

    /// <summary>
    /// Returns whether or not the given type is the given generic type (probably some kind of container)
    /// </summary>
    /// <param name="type"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool IsGenericType<T>(this Type type)
    {
        return type.IsGenericType(typeof(T));
    }

    /// <summary>
    /// Check if given type is some kind of dictionary
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsGenericDictionary(this Type type) => type is IDictionary || type.IsGenericType(typeof(IDictionary<,>)) || type.IsGenericType(typeof(IDictionary));

    /// <summary>
    /// Check if given type is some kind of enumerable
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsGenericEnumerable(this Type type) => type.IsGenericType(typeof(IEnumerable)) && !type.IsGenericDictionary();

    /// <summary>
    /// Simple method for sending the given object as json over the given websocket
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="sendItem"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static Task SendObjectAsync<T>(this WebSocket ws, T sendItem, WebSocketMessageType type = WebSocketMessageType.Text, 
        CancellationToken? token = null)
    {
        var realToken = token ?? CancellationToken.None;
        return ws.SendAsync(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(sendItem)), type, true, realToken);
    }

    /// <summary>
    /// Simple method for reading an object of the given type over the given websocket, assuming it's in json format.
    /// Can throw many types of exceptions, from connection to json parse errors to request exceptions
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="buffer"></param>
    /// <param name="token"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<T> ReceiveObjectAsync<T>(this WebSocket ws, Stream? buffer, CancellationToken? token = null)
    {
        var realBuffer = buffer ?? new MemoryStream();

        try
        {
            var tempBuffer = new ArraySegment<byte>(new byte[ReceiveObjectAsyncBufferSize]);
            //var tempBuffer = new byte[ReceiveObjectAsyncBufferSize];
            var realToken = token ?? CancellationToken.None;

            //Use the whole buffer! Hope you don't mind!
            realBuffer.SetLength(0);
            realBuffer.Position = 0; 

            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(tempBuffer, realToken);

                if(result.MessageType == WebSocketMessageType.Text) //If statement optimization, don't go checking the other paths
                    await realBuffer.WriteAsync(tempBuffer.Array!, tempBuffer.Offset, result.Count);
                else if(result.MessageType == WebSocketMessageType.Binary)
                    throw new RequestException($"Client sent unsupported message type: binary");
                else if(result.MessageType == WebSocketMessageType.Close)
                    throw new ClosedException($"Client closed connection manually");
            }
            while(result.EndOfMessage != true);

            realBuffer.Position = 0; 
            using var reader = new StreamReader(realBuffer, leaveOpen : true);
            var readString = await reader.ReadToEndAsync();
            try {
                return JsonConvert.DeserializeObject<T>(readString) ?? throw new RequestException($"Couldn't parse an object of type {typeof(T)}");
            }
            catch(Exception ex) {
                throw new RequestException($"Readstring failure, string = '{readString}'", ex);
            }
        }
        finally
        {
            if(buffer == null)
                await realBuffer.DisposeAsync();
        }
    }

    /// <summary>
    /// Run the given function by creating a file from the filestream, passing the path to the runnable, then
    /// removing the temp file afterwards
    /// </summary>
    /// <param name="fileData"></param>
    /// <param name="runnable"></param>
    /// <returns></returns>
    public static async Task TemporaryFileTask(this Stream fileData, string tempFolder, Func<string, Task> runnable)
    {
        var tempFile = Path.GetFullPath(Path.Combine(tempFolder, Guid.NewGuid().ToString().Replace("-", "")));
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

}
