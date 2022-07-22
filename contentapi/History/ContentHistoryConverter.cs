using System.IO.Compression;
using contentapi.data;
using contentapi.Db;
using Newtonsoft.Json;

namespace contentapi.History;

public class HistoryConverter : IHistoryConverter
{
    protected ILogger logger;

    public HistoryConverter(ILogger<HistoryConverter> logger)
    {
        this.logger = logger;
    }

    public async Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long user, UserAction action, DateTime? specificTime = null)
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

    public Task<ContentSnapshot> HistoryToContentAsync(ContentHistory history)
    {
        if(history.snapshotVersion == 1)
            return ExtractV1Snapshot<ContentSnapshot>(history.snapshot);
        else 
            throw new InvalidOperationException($"Unknown snapshot version {history.snapshotVersion}");
    }

    public async Task<byte[]> GetV1Snapshot<T>(T content)
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

    public async Task<T> ExtractV1Snapshot<T>(byte[] snapshot)
    {
        using var memstream = new MemoryStream(snapshot);
        using var gzip = new GZipStream(memstream, CompressionMode.Decompress, true);
        using var outputStream = new MemoryStream();
        await gzip.CopyToAsync(outputStream);
        var result = outputStream.ToArray();
        var jsonString = System.Text.Encoding.UTF8.GetString(result);
        return JsonConvert.DeserializeObject<T>(jsonString) ?? throw new InvalidOperationException($"Couldn't convert decompressed snapshot v1 to {typeof(T)}!");
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
        current.history = JsonConvert.SerializeObject(snapshots);
    }

    public List<CommentSnapshot> GetCommentHistory(Message current)
    {
        if(string.IsNullOrEmpty(current.history))
            return new List<CommentSnapshot>();

        return JsonConvert.DeserializeObject<List<CommentSnapshot>>(current.history) ??
            throw new InvalidOperationException("Couldn't convert history to list of snapshots!");
    }
}