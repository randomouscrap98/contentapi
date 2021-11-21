using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace contentapi.Services.Extensions
{
    //No interface yet because I don't really know what it will need
    public static class WebsocketExtensions
    {
        public const int BufferSize = 4096;
        public static TimeSpan SendPolling = TimeSpan.FromMilliseconds(100);

        public static async Task<WebSocketReceiveResult> ReceiveAsync(this WebSocket socket, Stream stream, CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            WebSocketReceiveResult response = null;

            do {
                response = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                await stream.WriteAsync(buffer, 0, response.Count);
            } while (!response.EndOfMessage);

            return response;
        }

        //They say only ONE person can write to a websocket at a time.
        public static async Task SendAsync(this WebSocket socket, byte[] data, WebSocketMessageType type, bool endOfMessage, CancellationToken token)
        {
            while(true)
            {
                token.ThrowIfCancellationRequested();

                if (Monitor.TryEnter(socket, SendPolling))
                {
                    try
                    {
                        await socket.SendAsync(new ArraySegment<byte>(data), type, endOfMessage, token);
                        return;
                    }
                    finally
                    {
                        Monitor.Exit(socket);
                    }
                }
            }
        }

        public static async Task CloseAsync(this WebSocket socket, WebSocketReceiveResult result, CancellationToken token)
        {
            await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, token);
        }
    }
}