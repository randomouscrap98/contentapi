using contentapi.data;
using contentapi.data.Views;
using contentapi.Db;
using contentapi.Search;
using contentapi.Utilities;

namespace contentapi;

public class PermissionService : IPermissionService
{
    protected ILogger logger;
    protected IViewTypeInfoService typeInfoService;

    public PermissionService (ILogger<PermissionService> logger, IViewTypeInfoService typeInfoService)
    {
        this.typeInfoService = typeInfoService;
        this.logger = logger;
    }

    public List<long> GetPermissionIdsForUser(UserView requester)
    {
        var groups = new List<long> { 0, requester.id };
        groups.AddRange(requester.groups);
        return groups;
    }

    public string ActionToString(UserAction action)
    {
        switch(action)
        {
            case UserAction.create: return "C";
            case UserAction.read: return "R";
            case UserAction.update: return "U";
            case UserAction.delete: return "D";
            default: throw new ArgumentException($"Unknown permission type {action}");
        }
    }

    public UserAction StringToAction(string action)
    {
        switch(action)
        {
            case "C": return UserAction.create;
            case "R": return UserAction.read;
            case "U": return UserAction.update;
            case "D": return UserAction.delete;
            default: throw new ArgumentException($"Unknown permission type {action}");
        }
    }

    public string ActionToColumn(UserAction action)
    {
        switch(action)
        {
            case UserAction.create: return nameof(ContentPermission.create);
            case UserAction.read: return nameof(ContentPermission.read);
            case UserAction.update: return nameof(ContentPermission.update);
            case UserAction.delete: return nameof(ContentPermission.delete);
            default: throw new ArgumentException($"Unknown permission type {action}");
        }
    }

    public Dictionary<long, string> ResultToPermissions(IEnumerable<dynamic> permissions)
    {
        return permissions.ToDictionary(
                x => (long)x.userId, y => $"{(y.create==1?"C":"")}{(y.read==1?"R":"")}{(y.update==1?"U":"")}{(y.delete==1?"D":"")}");
    }

    public List<Db.ContentPermission> PermissionsToDb(ContentView content)
    {
        return content.permissions.Select(x => new Db.ContentPermission {
            userId = x.Key,
            contentId = content.id,
            create = x.Value.Contains('C'),
            read = x.Value.Contains('R'),
            update = x.Value.Contains('U'),
            delete = x.Value.Contains('D')
        }).ToList();
    }

    public bool CanUserStatic(UserView requester, UserAction action, Dictionary<long, string> viewPerms)
    {
        var actionString = ActionToString(action);

        foreach(var id in GetPermissionIdsForUser(requester))
        {
            if(viewPerms.ContainsKey(id) && viewPerms[id].Contains(actionString))
                return true;
        }

        return false;
    }
}