using contentapi.data;
using contentapi.Db;

namespace contentapi.History;

public interface IHistoryConverter
{
    Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long userId, UserAction action, DateTime? specificTime = null);
    Task<ContentSnapshot> HistoryToContentAsync(ContentHistory history);
    void AddCommentHistory(CommentSnapshot snapshot, Message current);
    void SetCommentHistory(List<CommentSnapshot> snapshots, Message current);
    List<CommentSnapshot> GetCommentHistory(Message current);
}
