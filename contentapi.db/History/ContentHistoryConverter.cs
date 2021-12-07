using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace contentapi.Db.History
{
    public class HistoryConverter : IHistoryConverter
    {
        protected ILogger logger;

        public HistoryConverter(ILogger<HistoryConverter> logger)
        {
            this.logger = logger;
        }

        public async Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long user, UserAction action, DateTime? specificTime)
        {
            var history = new ContentHistory()
            {
                action = action,
                createDate = specificTime ?? DateTime.UtcNow,
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
            using (var memstream = new MemoryStream())
            {
                using (var gzip = new GZipStream(memstream, CompressionLevel.Fastest, true))
                {
                    await gzip.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                }
                //Apparently you HAVE to do it afterwards? IDK
                return memstream.ToArray();
            }
        }

        public void AddCommentHistory(CommentSnapshot snapshot, Comment current)
        {
            List<CommentSnapshot> snapshots = new List<CommentSnapshot>();

            if(!string.IsNullOrWhiteSpace(current.history))
                snapshots = JsonConvert.DeserializeObject<List<CommentSnapshot>>(current.history);
            
            snapshots.Add(snapshot);

            SetCommentHistory(snapshots, current);
        }

        public void SetCommentHistory(List<CommentSnapshot> snapshots, Comment current)
        {
            current.history = JsonConvert.SerializeObject(snapshots);
        }
    }
}