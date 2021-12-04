using System.Data;
using AutoMapper;
using contentapi.Db;
using contentapi.Db.History;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.Main;

public class DbWriter
{
    protected ILogger logger;
    protected IGenericSearch searcher;
    protected IDbConnection dbcon;
    protected ITypeInfoService typeInfoService;
    protected IMapper mapper;

    public DbWriter(ILogger<DbWriter> logger, IGenericSearch searcher, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, IMapper mapper)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.dbcon = connection.Connection;
        this.typeInfoService = typeInfoService;
        this.mapper = mapper;
    }

    //Consider how to write a user? Users are one of the only things that have private information, so 
    //perhaps it needs its own unique writer endpoint

    //public async Task WriteRaw<T>(T dbItem) where T : class
    //{
    //    await dbcon.InsertAsync(dbItem);
    //}


    //Avoid generic constraints unless they're required by the underlying system. Just restricting because it's
    //"all" you expect is bad, remember all the problems you had before with that
    public async Task<T> WriteAsync<T>(T view, long requestUserId) where T : class, IIdView
    {
        if(requestUserId <= 0)
            throw new InvalidOperationException("Can't use system UIDs right now for writes! It's reserved!");

        var requester = new UserView() { };
        
        try
        {
            //This apparently throws an exception if it fails
            requester = await searcher.GetById<UserView>(RequestType.user, requestUserId);
        }
        catch(Exception ex)
        {
            logger.LogWarning($"Error while looking up writer requester: {ex}");
            throw new ArgumentException($"Unknown request user {requestUserId}");
        }

        UserAction action = view.id == 0 ? UserAction.create : UserAction.update;
        await PermissionPrecheck(view, requester, action);

        long id = 0;
        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException($"Type {typeof(T).Name} has no associated request type for querying, don't know how to write! This usually means this type is a complex type that doesn't represent a database object, and thus the API has a configuration error!");

        //Ok at this point, we know everything is good to go, there shouldn't be ANY MORE permission checks required past here!
        if(view is ContentView)
        {
            id = await WriteContent(view as ContentView ?? throw new InvalidOperationException("Somehow, ContentView couldn't be cast to ContentView??"), requester);
        }
        else
        {
            throw new ArgumentException($"Don't know how to write type {typeof(T).Name} to the database!");
        }

        return await searcher.GetById<T>(requestType, id);
    }

    public async Task<long> WriteContent(ContentView view, UserView requester)
    {
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            //Need to convert views to db content... how to do so without a big mess?

            //Regardless of what we're doing, need to convert view into a bunch of keywords, permissions, values, and the content itself
            return 0;
        }
    }

    public ContentSnapshot CreateSnapshotFromBaseContent(Db.Content content, ContentView originalView)
    {
        var snapshot = mapper.Map<ContentSnapshot>(content);

        snapshot.keywords = originalView.keywords.Select(x => new Db.ContentKeyword {
            value = x,
            contentId = originalView.id
        }).ToList();

        snapshot.values = originalView.values.Select(x => new Db.ContentValue {
            key = x.Key,
            value = x.Value,
            contentId = originalView.id
        }).ToList();

        return snapshot;
    }

    public async Task PermissionPrecheck<T>(T view, UserView requester, UserAction action) where T : class, IIdView
    {
        //Each type needs a different kind of check
        if(view is ContentView)
        {
            //Create is special, because we need the parent create permission
            if(action == UserAction.create)
            {
                //Only supers can create modules, but after that, permissions are based on their internal permissions.
                if(view is ModuleView && !requester.super)
                    throw new ForbiddenException($"Only supers can create modules!");

                var cView = view as ContentView ?? throw new InvalidOperationException("Somehow, ContentView could not be cast to a ContentView");

                //Only need to check parent for create if it's actually a place! This is because we treat non-valid 
                //ids as orphaned pages
                if (cView.parentId > 0)
                {
                    if(!(await UserCan(requester, action, cView.parentId)))
                        throw new ForbiddenException($"User {requester.id} can't '{action}' content in parent {cView.parentId}!");
                }
            }
        }
        else if(view is CommentView)
        {
            var cView = view as CommentView ?? throw new InvalidOperationException("Somehow, CommentView could not be cast to a CommentView");

            //Create is special, because we need the parent create permission
            if(action == UserAction.create)
            {
                //Can't post in invalid locations! So we check ANY contentId passed in, even if it's invalid
                if(!(await UserCan(requester, action, cView.contentId)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' comments in content {cView.contentId}!");
            }
            //All other non-read actions can only ber performed the original user or supers
            else if(action != UserAction.read)
            {
                if(!requester.super || cView.createUserId != requester.id)
                    throw new ForbiddenException($"Only the original poster and supers can modify existing comments!");
            }
        }
        else 
        {
            //Be SAFER than sorry! All views when created are by default NOT writable!
            throw new ForbiddenException($"View of type {view.GetType().Name} cannot be user-modified!");
        }
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
    public async Task<bool> UserCan(UserView requester, UserAction action, long thing)
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
        return (await dbcon.ExecuteScalarAsync<int>(@$"(select 
             from {typeInfo.table} 
             where {nameof(ContentPermission.contentId)} = @contentId
               and {nameof(ContentPermission.userId)} in @requesters 
               and `{checkCol}` = 1
            )")) > 0;
    }
}