using contentapi.Db;
using contentapi.Views;

namespace contentapi;

public interface IDbPermissionService
{
    List<long> GetPermissionIdsForUser(UserView requester);
    Task<bool> CanUserAsync(UserView requester, UserAction action, long thing);
}