using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi;

public class PermissionService : IPermissionService
{
    protected ILogger logger;
    protected ITypeInfoService typeInfoService;

    public PermissionService (ILogger<PermissionService> logger, ITypeInfoService typeInfoService)
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