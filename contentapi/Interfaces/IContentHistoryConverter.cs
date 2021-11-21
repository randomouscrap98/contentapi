using contentapi.Db;

namespace contentapi;

public interface IContentHistoryConverter
{
    Task<ContentHistory> ContentToHistoryAsync(ContentSnapshot content, long userId, UserAction action, DateTime? specificTime = null);
}
