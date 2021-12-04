using contentapi.Db;

namespace contentapi.Db.History;

public interface IContentHistoryConverter
{
    Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long userId, UserAction action, DateTime? specificTime = null);
}
