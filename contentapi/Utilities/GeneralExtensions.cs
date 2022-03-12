using System.Net.WebSockets;
using Newtonsoft.Json;

namespace contentapi.Utilities;

public static class GeneralExtensions
{
    public const int ReceiveObjectAsyncBufferSize = 4096;

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
            var tempBuffer = new byte[ReceiveObjectAsyncBufferSize];
            var realToken = token ?? CancellationToken.None;

            //Use the whole buffer! Hope you don't mind!
            realBuffer.Position = 0; 

            WebSocketReceiveResult result;

            do
            {
                result = await ws.ReceiveAsync(tempBuffer, realToken);

                if(result.MessageType == WebSocketMessageType.Text) //If statement optimization, don't go checking the other paths
                    await realBuffer.WriteAsync(tempBuffer, 0, result.Count);
                else if(result.MessageType == WebSocketMessageType.Binary)
                    throw new RequestException($"Client sent unsupported message type: binary");
                else if(result.MessageType == WebSocketMessageType.Close)
                    throw new ClosedException($"Client closed connection manually");
            }
            while(result.EndOfMessage != true);

            realBuffer.Position = 0; 
            using var reader = new StreamReader(realBuffer, leaveOpen : true);
            var readString = await reader.ReadToEndAsync();
            return JsonConvert.DeserializeObject<T>(readString) ?? throw new RequestException($"Couldn't parse an object of type {typeof(T)}");
        }
        finally
        {
            if(buffer == null)
                await realBuffer.DisposeAsync();
        }
    }
}
