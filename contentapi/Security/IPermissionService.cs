using contentapi.data.Views;
using contentapi.Db;

namespace contentapi;

public interface IPermissionService
{
    List<long> GetPermissionIdsForUser(UserView requester);
    bool CanUserStatic(UserView requester, UserAction action, Dictionary<long, string> viewPerms);

    string ActionToString(UserAction action);
    UserAction StringToAction(string action);
    string ActionToColumn(UserAction action);

    Dictionary<long, string> ResultToPermissions(IEnumerable<dynamic> permissions);
    List<Db.ContentPermission> PermissionsToDb(ContentView content);
}