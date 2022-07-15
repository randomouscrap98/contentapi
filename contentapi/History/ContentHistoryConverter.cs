using System.IO.Compression;
using System.Text.Json;
using contentapi.data;
using contentapi.Db;
using contentapi.Utilities;

namespace contentapi.History;

public class HistoryConverter : IHistoryConverter
{
    protected ILogger logger;
    protected IJsonService jsonService;

    public HistoryConverter(ILogger<HistoryConverter> logger, IJsonService jsonConvert)
    {
        this.logger = logger;
        this.jsonService = jsonConvert;
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

    public async Task<byte[]> GetV1Snapshot<T>(T content)
    {
        //Snapshot this time is a simple compressed json object.
        //var jsonString = JsonConvert.SerializeObject(content);
        var jsonString = JsonSerializer.Serialize(content); //SerializeObject(content);
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

    public void AddCommentHistory(CommentSnapshot snapshot, Message current)
    {
        List<CommentSnapshot> snapshots = new List<CommentSnapshot>();

        if(!string.IsNullOrWhiteSpace(current.history))
            snapshots = GetCommentHistory(current);
        
        snapshots.Add(snapshot);

        SetCommentHistory(snapshots, current);
    }

    public void SetCommentHistory(List<CommentSnapshot> snapshots, Message current)
    {
        current.history = jsonService.Serialize(snapshots);
    }

    public List<CommentSnapshot> GetCommentHistory(Message current)
    {
        if(string.IsNullOrEmpty(current.history))
            return new List<CommentSnapshot>();

        return jsonService.Deserialize<List<CommentSnapshot>>(current.history) ??
            throw new InvalidOperationException("Couldn't convert history to list of snapshots!");
    }
}