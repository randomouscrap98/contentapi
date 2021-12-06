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
    protected IContentHistoryConverter historyConverter;

    public DbWriter(ILogger<DbWriter> logger, IGenericSearch searcher, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService, IMapper mapper, IContentHistoryConverter historyConverter)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.dbcon = connection.Connection;
        this.typeInfoService = typeInfoService;
        this.mapper = mapper;
        this.historyConverter = historyConverter;
    }

    //Consider how to write a user? Users are one of the only things that have private information, so 
    //perhaps it needs its own unique writer endpoint

    public async Task<UserView> GetRequestUser(long requestUserId)
    {
        if(requestUserId <= 0)
            throw new ForbiddenException("Can't use system UIDs right now for writes! It's reserved!");

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

        return requester;
    }

    //Avoid generic constraints unless they're required by the underlying system. Just restricting because it's
    //"all" you expect is bad, remember all the problems you had before with that
    public async Task<T> WriteAsync<T>(T view, long requestUserId) where T : class, IIdView, new()
    {
        return await GenericWorkAsync(view, await GetRequestUser(requestUserId), view.id == 0 ? UserAction.create : UserAction.update);
    }

    public async Task<T> DeleteAsync<T>(long id, long requestUserId) where T : class, IIdView, new()
    {
        return await GenericWorkAsync(new T() { id = id }, await GetRequestUser(requestUserId), UserAction.delete);
    }

    /// <summary>
    /// Throws "ForbiddenException" for any kind of permission error that could arise for a modification given
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public async Task CheckPermissionsAsync<T>(T view, UserView requester, UserAction action) where T : class, IIdView, new()
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
    /// Inserts, updates, and deletes for any type should call THIS function
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public async Task<T> GenericWorkAsync<T>(T view, UserView requester, UserAction action) where T : class, IIdView, new()
    {
        CheckActionValidForView(action, view);
        await CheckPermissionsAsync(view, requester, action);

        long id = 0;
        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException($"Type {typeof(T)} has no associated request type for querying, don't know how to write! This usually means this type is a complex type that doesn't represent a database object, and thus the API has a configuration error!");

        //Now go get the existing if it's that kind of thing!
        T? existing = null;

        if(action == UserAction.update || action == UserAction.delete)
        {
            //This will throw an exception if we can't find it by id, so that's the end of that...
            existing = await searcher.GetById<T>(requestType, view.id);
        }

        //Ok at this point, we know everything is good to go, there shouldn't be ANY MORE checks required past here!
        if(view is ContentView)
        {
            id = await DatabaseWork_Content(new DbWorkUnit<ContentView>(
                view as ContentView ?? throw new InvalidOperationException("Somehow, ContentView couldn't be cast to ContentView??"), 
                requester, typeInfo, action, existing as ContentView));
        }
        else
        {
            throw new ArgumentException($"Don't know how to write type {typeof(T).Name} to the database!");
        }

        //The regardless, just go look up whatever work we just did, give the user the MOST up to date information, even if it's inefficient
        return await searcher.GetById<T>(requestType, id);
    }

    //Ensure the action and the view make sense for each other
    public void CheckActionValidForView(UserAction action, IIdView view)
    {
        if(action == UserAction.update || action == UserAction.delete)
        {
            if(view.id <= 0)
                throw new ArgumentException($"Alteration action '{action}' requires id, but no id was set in view!");
        }
        else if(action == UserAction.create)
        {
            if(view.id != 0)
                throw new ArgumentException($"Alteration action '{action} requires NO id set, but id was {view.id}");
        }
        else
        {
            //This one should just never happen since it's internal, so it's an InvalidOperationException instead
            throw new InvalidOperationException($"Unsupported action '{action}' inside modification context");
        }
    }


    /// <summary>
    /// Delete stuff of the given type associated with the given content. WARNING: THIS WORKS FOR COMMENTS, VOTES, WATCHES, ETC!!
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tsx"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task DeleteOldContentThings<T>(long id, IDbTransaction tsx)
    {
        var tinfo = typeInfoService.GetTypeInfo<T>();
        var parameters = new { id = id };
        var deleteCount = await dbcon.ExecuteAsync($"delete from {tinfo.table} where contentId = @id", parameters, tsx);
        logger.LogInformation($"Deleting {deleteCount} {typeof(T).Name} from content {id}");
    }

    /// <summary>
    /// Delete the values, keywords, and permissions associted with the given content. Note that these are the things
    /// which should be reset on content update.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tsx"></param>
    /// <returns></returns>
    public async Task DeleteOldContentAssociated(long id, IDbTransaction tsx)
    {
        await DeleteOldContentThings<ContentValue>(id, tsx);
        await DeleteOldContentThings<ContentKeyword>(id, tsx);
        await DeleteOldContentThings<ContentPermission>(id, tsx);
    }

    public AdminLog MakeContentLog(UserView requester, ContentView view, UserAction action)
    {
        return new AdminLog {
            text = $"User '{requester.username}'({requester.id}) {action}d '{view.name}'({view.id})",
            type = action == UserAction.create ? AdminLogType.contentCreate :  
                    action == UserAction.update ? AdminLogType.contentUpdate :
                    action == UserAction.delete ? AdminLogType.contentDelete :
                    throw new InvalidOperationException($"Unsupported admin log type {action}"),
            target = view.id,
            initiator = requester.id,
            createDate = DateTime.Now
        };
    }

    /// <summary>
    /// Use TypeInfo given to trnanslate all standard mapped fields from view to model.
    /// </summary>
    /// <param name="tinfo"></param>
    /// <param name="view"></param>
    /// <param name="dbModel"></param>
    /// <returns>Fields that were NOT mapped</returns>
    public List<string> SimpleViewDbMap(TypeInfo tinfo, object view, object dbModel)
    {
        //This can happen if our view type has no associated table
        if(tinfo.tableTypeProperties.Count == 0)
            throw new InvalidOperationException($"Typeinfo for type {tinfo.type}, table type {tinfo.tableType} didn't compute table type properties!");
    
        //Assume no properties will be mapped
        var unmapped = new List<string>(tinfo.tableTypeProperties.Keys);

        //We want to get as many fields as possible. If there are some we can't map, that's ok.
        foreach(var dbModelProp in tinfo.tableTypeProperties)
        {
            //These are ALWAYS dead ends: empty field remaps that have the same name as us are complicated....
            if(tinfo.fieldRemap.ContainsKey(dbModelProp.Key) && string.IsNullOrWhiteSpace(tinfo.fieldRemap[dbModelProp.Key]))
                continue;

            //The viewfield we might use
            var viewField = "";

            //FieldRemap maps view properties to db, but we want where OUR property is in the value
            var remap = tinfo.fieldRemap.FirstOrDefault(x => x.Value == dbModelProp.Key);

            //It's a field remap, might work that way... always trust this over the defaults
            if(!string.IsNullOrWhiteSpace(remap.Key))
                viewField = remap.Key;
            //Oh it's a queryable field, that works too. In that case, it's the same exact name
            else if(tinfo.queryableFields.Contains(dbModelProp.Key))
                viewField = dbModelProp.Key;

            //Only do the reassign if we found a viewField
            if(!string.IsNullOrWhiteSpace(viewField))
            {
                //Oh but somehow the view type doesn't have the field we thought it did? this shouldn't happen!
                if (!tinfo.properties.ContainsKey(viewField))
                    throw new InvalidOperationException($"Somehow, the typeinfo for {tinfo.type} didn't include a property for mapped field {viewField}");

                //Set the dbmodel property value to be the view's property. Type matching is NOT checked, please be careful!
                dbModelProp.Value.SetValue(dbModel, tinfo.properties[viewField].GetValue(view));
                unmapped.Remove(dbModelProp.Key);
            }
        }

        return unmapped;
    }

    /// <summary>
    /// This function handles any database modification for the given view, from inserts to updates to deletes.
    /// </summary>
    /// <param name="view"></param>
    /// <param name="requester"></param>
    /// <param name="typeInfo"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public async Task<long> DatabaseWork_Content(DbWorkUnit<ContentView> work) //ContentView view, UserView requester, TypeInfo typeInfo, UserAction action)
    {
        if(!work.typeInfo.type.IsAssignableTo(typeof(ContentView)))
            throw new InvalidOperationException($"TypeInfo given in DatabaseWork was for type '{work.typeInfo.type}', not '{typeof(ContentView)}'");
        
        //Need to convert views to db content... how to do so without a big mess?
        var content = new Db.Content();
        var unmapped = SimpleViewDbMap(work.typeInfo, work.view, content);

        //Don't forget to set the type appropriately!
        if(work.view is PageView)
            content.internalType = InternalContentType.page;
        else if(work.view is FileView)
            content.internalType = InternalContentType.file;
        else if(work.view is ModuleView)
            content.internalType = InternalContentType.module;
        else
            throw new InvalidOperationException($"Don't know how to write type {work.typeInfo.type}!");

        //Now modify the content before writing based on what we're doing. Some fields need to be reset/etc.
        if(work.action == UserAction.delete)
        {
            content.content = "";
            content.name = "";
            content.deleted = true;
            content.extra1 = null;
            content.internalType = InternalContentType.none;
            content.publicType = "";
            work.view.keywords.Clear();
            work.view.values.Clear();
            work.view.permissions.Clear();
        }
        else if(work.action == UserAction.update)
        {
            var existing = work.existing ?? throw new InvalidOperationException("Delete specified, but no existing view looked up in database for snapshot!");
            content.createUserId = existing.createUserId;
            content.createDate = existing.createDate;
        }
        else if(work.action == UserAction.create)
        {
            content.createDate = DateTime.Now;
            content.createUserId = work.requester.id;
        }

        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
                work.view.id = await dbcon.InsertAsync(content, tsx);
            else if(work.action == UserAction.update || work.action == UserAction.delete)
                await dbcon.UpdateAsync(content, tsx);
            else 
                throw new InvalidOperationException($"Can't perform action {work.action} in WriteContent!");

            //Now that we have a content and got the ID for it, we can produce the snapshot used for history writing
            var snapshot = CreateSnapshotFromBaseContent(content, work.view);

            //Note: when deleting, don't want to write ANY extra data, regardless of what's there!
            if(work.action != UserAction.delete)
            {
                //These insert entire lists.
                await dbcon.InsertAsync(snapshot.values, tsx);
                await dbcon.InsertAsync(snapshot.permissions, tsx);
                await dbcon.InsertAsync(snapshot.keywords, tsx);
            }

            //Regardless of what we're doing (create/delete/update), need to convert snapshot to the history item to insert.
            var history = await historyConverter.ContentToHistoryAsync(snapshot, work.requester.id, work.action);

            await dbcon.InsertAsync(history, tsx);

            var adminLog = MakeContentLog(work.requester, work.view, work.action);
            await dbcon.InsertAsync(adminLog, tsx);

            tsx.Commit();

            logger.LogDebug(adminLog.text); //The admin log actually has the log text we want!

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    /// <summary>
    /// Create a ContentSnapshot object using an existing content object, plus a view that presumably has the rest of the fields.
    /// This is done like this because all the associated snapshot values must be linked to a valid ID, and we figure only a 
    /// real "Content" object would have this information, and mapping occurs based on the values in a Content object, so just
    /// give us a real, pre-filled-out content!
    /// </summary>
    /// <param name="content"></param>
    /// <param name="originalView"></param>
    /// <returns></returns>
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

        snapshot.permissions = originalView.permissions.Select(x => new Db.ContentPermission {
            userId = x.Key,
            contentId = originalView.id,
            create = x.Value.Contains('C'),
            read = x.Value.Contains('R'),
            update = x.Value.Contains('U'),
            delete = x.Value.Contains('D')
        }).ToList();

        return snapshot;
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