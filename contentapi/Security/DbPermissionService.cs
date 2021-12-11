using System.Data;
using contentapi.Db;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;

namespace contentapi;

public class DbPermissionService : IDbPermissionService
{
    protected ILogger logger;
    protected ITypeInfoService typeInfoService;
    protected IDbConnection dbcon;

    public DbPermissionService (ILogger<DbPermissionService> logger, ContentApiDbConnection conwrap, ITypeInfoService typeInfoService)
    {
        this.typeInfoService = typeInfoService;
        this.logger = logger;
        this.dbcon = conwrap.Connection;
    }

    public List<long> GetPermissionIdsForUser(UserView requester)
    {
        var groups = new List<long> { 0, requester.id };
        groups.AddRange(requester.groups);
        return groups;
    }

    /// <summary>
    /// Whether the given already-looked-up user is allowed to perform the given action to the given "thing" by id. 
    /// </summary>
    /// <remarks>
    /// This is ONLY permission based, and thus does not have the additional logic required for specific types. Because
    /// of this, the entity id is enough to look up permissions
    /// </remarks>
    /// <param name="requester"></param>
    /// <param name="action"></param>
    /// <param name="thing"></param>
    /// <returns></returns>
    public async Task<bool> CanUserAsync(UserView requester, UserAction action, long thing)
    {
        //Supers can update/insert/delete anything they want, but they can't read secret things.
        if(action != UserAction.read && requester.super)
            return true;

        var typeInfo = typeInfoService.GetTypeInfo<ContentPermission>();
        string checkCol = "";

        switch(action)
        {
            case UserAction.create: checkCol = nameof(ContentPermission.create); break;
            case UserAction.read: checkCol = nameof(ContentPermission.read); break;
            case UserAction.update: checkCol = nameof(ContentPermission.update); break;
            case UserAction.delete: checkCol = nameof(ContentPermission.delete); break;
            default: throw new ArgumentException($"Unknown permission type {action}");
        }

        //Just about as optimized as I can make it: a raw query that only returns a count,
        //AND the contentId search is from an index so... at most it'll be like log2(n) + C where
        //C is the amount of users defined in the content permission set, and N is the total amount
        //of pages. So if we had oh I don't know, 2 billion pages, it might take like 40 iterations.
        return (await dbcon.ExecuteScalarAsync<int>(@$"select count(*)
             from {typeInfo.table} 
             where {nameof(ContentPermission.contentId)} = @contentId
               and {nameof(ContentPermission.userId)} in @requesters 
               and `{checkCol}` = 1
            ", new { contentId = thing, requesters = GetPermissionIdsForUser(requester) })) > 0;
    }
}