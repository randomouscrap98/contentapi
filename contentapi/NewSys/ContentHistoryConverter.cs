using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using contentapi.Db;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace contentapi
{
    public interface IContentHistoryConverter
    {
        Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long userId, UserAction action, DateTime? specificTime = null);
    }

    public class ContentHistoryConverter : IContentHistoryConverter
    {
        protected ILogger logger;

        public ContentHistoryConverter (ILogger<ContentHistoryConverter> logger)
        {
            this.logger = logger;
        }

        public async Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long user, UserAction action, DateTime? specificTime)
        {
            var history = new ContentHistory()
            {
                action = action, 
                createDate = specificTime ?? DateTime.Now,
                createUserId = user,
                contentId = content.id,
                snapshotVersion = 1,
                snapshot = await GetV1Snapshot(content)
            };

            return history;
        }

        public async Task<byte[]> GetV1Snapshot(ContentSnapshot content)
        {
            //Snapshot this time is a simple compressed json object.
            var jsonString = JsonConvert.SerializeObject(content);
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);
            using(var memstream = new MemoryStream(jsonBytes))
            {
                using(var gzip = new GZipStream(memstream, CompressionLevel.Optimal))
                {
                    await gzip.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                    return memstream.ToArray();
                }
            }
        }
    }
}