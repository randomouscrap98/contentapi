using System.Data;
using System.Text.RegularExpressions;
using AutoMapper;
using contentapi.Db;
using contentapi.Live;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.data.Views;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using contentapi.data;
using contentapi.History;

namespace contentapi.Main;

public class DbWriterConfig
{
   public int AutoHashChars {get;set;} = 5;
   public int AutoHashMaxRetries {get;set;} = 50;
   public string HashRegex {get;set;} = @"^[a-z\-]+$";
   public int HashMinLength {get;set;} = 8;
   public int HashMaxLength {get;set;} = 32;
   public List<string> ReservedModuleNames {get;set;} = new List<string> {
       "system"
   };
}

public class DbWriter : IDbWriter
{
    protected ILogger logger;
    protected IGenericSearch searcher;
    protected IDbConnection dbcon;
    protected IViewTypeInfoService typeInfoService;
    protected IMapper mapper;
    protected IHistoryConverter historyConverter;
    protected IPermissionService permissionService;
    protected ILiveEventQueue eventQueue;
    protected DbWriterConfig config;
    protected IRandomGenerator rng;

    //TODO: THIS IS A TEMPORARY INJECTION! I don't think I want the writer dependent on the user service, since
    //the user service MAY someday be dependent on the writer!!
    protected IUserService userService;

    protected static readonly SemaphoreSlim hashLock = new SemaphoreSlim(1, 1);

    public List<RequestType> TrueDeletes = new List<RequestType> {
        RequestType.watch,
        RequestType.vote,
        RequestType.uservariable
    };

    public DbWriter(ILogger<DbWriter> logger, IGenericSearch searcher, IDbConnection connection,
        IViewTypeInfoService typeInfoService, IMapper mapper, IHistoryConverter historyConverter,
        IPermissionService permissionService, ILiveEventQueue eventQueue, DbWriterConfig config,
        IRandomGenerator rng, IUserService userService)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.dbcon = connection;
        this.typeInfoService = typeInfoService;
        this.mapper = mapper;
        this.historyConverter = historyConverter;
        this.permissionService = permissionService;
        this.eventQueue = eventQueue;
        this.config = config;
        this.rng = rng;
        this.userService = userService;
    
        //Preemptively open this, we know us (as a writer) SHOULD BE short-lived, so...
        this.dbcon.Open();
    }

    public void Dispose()
    {
        searcher.Dispose();
        dbcon.Dispose();
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
    public virtual async Task<T> WriteAsync<T>(T view, long requestUserId, string? message = null) where T : class, IIdView, new()
    {
        return await GenericWorkAsync(view, await GetRequestUser(requestUserId), view.id == 0 ? UserAction.create : UserAction.update, message);
    }

    public virtual async Task<T> DeleteAsync<T>(long id, long requestUserId, string? message = null) where T : class, IIdView, new()
    {
        //To properly delete, you actually need to lookup the view
        var tInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = tInfo.requestType ?? throw new InvalidOperationException($"Tried to delete type {typeof(T)} but type had no request type for searching!");
        return await GenericWorkAsync(await searcher.GetById<T>(requestType , id, true), await GetRequestUser(requestUserId), UserAction.delete, message);
    }

    /// <summary>
    /// Throws "ForbiddenException" for any kind of permission error that could arise for a modification given. ALSO perform other
    /// validation in here!
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public async Task ValidateWorkGeneral<T>(T view, T? existing, UserView requester, UserAction action) where T : class, IIdView, new()
    {
        //regardless, always go get ban data
        var requesterBan = (await searcher.SearchSingleTypeUnrestricted<BanView>(new SearchRequest()
        {
            type = nameof(RequestType.ban),
            fields = "*",
            query = $"!activebans() and {nameof(BanView.bannedUserId)} = @uid"
        }, new Dictionary<string, object> { { "uid", requester.id }})).FirstOrDefault();

        //Each type needs a different kind of check
        if(view is ContentView)
        {
            var cView = view as ContentView ?? throw new InvalidOperationException("Somehow, ContentView could not be cast to a ContentView");
            var exView = existing as ContentView;

            //Don't check certain fields for "don't allow change" (like type) because the later systems do it for you
            //(with writable:preserve attributes and all that)

            //Only need to check parent for create if it's actually a place! This is because we treat non-valid 
            //ids as orphaned pages. We care about the user's ability to "create" pages in the parent when a new view is created OR when
            //a view is updated and being moved to a different parent.
            if((action == UserAction.create || (action == UserAction.update && cView.parentId != exView!.parentId)) && (cView.parentId > 0))
            {
                if(!(await CanUserAsync(requester, UserAction.create, cView.parentId)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' content in parent {cView.parentId}!");
            }

            //Create is special, because we only check the parent for create (and we did that earlier)
            if(action == UserAction.create)
            {
                //Only supers can create modules, but after that, permissions are based on their internal permissions.
                if(cView.contentType == InternalContentType.module && !requester.super)
                    throw new ForbiddenException($"Only supers can create modules!");
                
                if(cView.contentType == InternalContentType.none)
                    throw new ForbiddenException($"You can't create content type '{cView.contentType}'!");
            }
            else if(action != UserAction.read)
            {
                //We generally assume that the views and such have proper fields by the time it gets to us...
                if(!(await CanUserAsync(requester, action, cView.id)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' content {cView.id}!");
            }

            if(cView.contentType == InternalContentType.module)
            {
                if (config.ReservedModuleNames.Select(x => x.ToLower()).Contains(cView.name.ToLower()))
                    throw new ForbiddenException($"Module name {cView.name} is reserved!");
                
                var exmod = await searcher.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
                {
                    type = "content",
                    fields = "id,name,contentType,deleted",
                    query = "name = @name and contentType = @type and id <> @id and !notdeleted()"
                }, new Dictionary<string, object> {
                    { "name", cView.name },
                    { "type", InternalContentType.module },
                    { "id", cView.id }
                });

                if(exmod.Count > 0)
                    throw new RequestException($"A module already exists with name {cView.name}");
            }

            //WARN : NO PARENTID VALIDATION!

            //Now for general validation
            await ValidateBanOnContent(cView.id, requesterBan);
            await ValidatePermissionFormat(cView.permissions);

            if(cView.deleted)
                throw new RequestException("Don't delete content by setting the deleted flag!");
        }
        else if(view is MessageView)
        {
            var cView = view as MessageView ?? throw new InvalidOperationException("Somehow, MessageView could not be cast to a MessageView");

            //This check USED TO be only create, but now that we can rethread, it must ALWAYS be checked!

            //No orphaned comments, so the parent MUST exist! This is an easy check. You will get a "notfound" exception
            var parent = await searcher.GetById<ContentView>(RequestType.content, cView.contentId, true);

            //Can't post in invalid locations! So we check ANY contentId passed in, even if it's invalid
            if (parent == null || !(await CanUserAsync(requester, UserAction.create, cView.contentId)))
                throw new ForbiddenException($"User {requester.id} can't '{action}' comments in content {cView.contentId}!");

            //Modification actions
            if(action == UserAction.update || action == UserAction.delete)
            {
                var exView = existing as MessageView ?? throw new InvalidOperationException("Permissions wasn't given the old view during check!");

                if(!(requester.super || exView.createUserId == requester.id))
                    throw new ForbiddenException($"Only the original poster and supers can modify existing comments!");

                if(exView.module != null)
                    throw new ForbiddenException($"You cannot modify module messages!");
            }

            await ValidateBanOnContent(cView.contentId, requesterBan);

            if(cView.deleted)
                throw new RequestException("Don't delete comments by setting the deleted flag!");
        }
        else if(view is UserView)
        {
            var uView = (view as UserView)!;
            var exView = existing as UserView;

            //Don't check certain fields for "don't allow change" (like type) because the later systems do it for you
            //(with writable:preserve attributes and all that)
            
            //This ensures that the "super" field, no matter where it is, is only modifyable with supers.
            if((uView.super || exView?.super == true) && !requester.super)
                throw new ForbiddenException("You can't minipulate the super field through this endpoint unless you're a super user!");

            if(uView.deleted)
                throw new RequestException("Don't delete users by setting the deleted flag!");

            //Never allow usersInGroup to be set for non-group items
            if(uView.type != UserType.group && uView.usersInGroup.Count > 0)
                throw new RequestException("You can't add users to a non-group user!");
            
            //Just unconditionally check for valid username. This will prevent people from modifying other fields if their
            //username is bad, but that's fine I think, they SHOULD change their username if so
            await userService.CheckValidUsernameAsync(uView.username, uView.id);

            if(action != UserAction.delete)
            {
                //Go lookup the avatar, make sure they can't set something junk
                if(uView.avatar != Constants.DefaultHash)
                {
                    var fileData = (await searcher.GetByField<ContentView>(RequestType.content, "hash", uView.avatar)).FirstOrDefault();
                    if(fileData == null || fileData.id == 0)
                        throw new RequestException($"Couldn't find avatar {uView.avatar}");
                }
            }

            if (uView.type == UserType.user)
            {
                //Users aren't created here except by supers? This might change sometime
                if(action == UserAction.create)
                    throw new ForbiddenException("Nobody can create users outside of registration!");

                if(action == UserAction.update)
                {
                    //This luckily encompasses groups too, so nobody but supers can edit groups too
                    if(requester.id != uView.id && !requester.super)
                        throw new ForbiddenException("You cannot modify users other than yourself unless you're a super user!");
                }

                if(action == UserAction.delete)
                {
                    if(!requester.super)
                        throw new ForbiddenException("Only super users can delete users!");
                }
            }
            else if (uView.type == UserType.group)
            {
                await ValidateUsers(uView.usersInGroup, requester);

                //You can only work with existing groups if you created them or you're a super user
                if(action == UserAction.update || action == UserAction.delete)
                {
                    if (!(requester.id == exView!.createUserId || requester.super))
                        throw new ForbiddenException("You're not allowed to modify this group!");
                }
            }
            else
            {
                throw new RequestException($"You cannot do anything with users of type {uView.type}!");
            }
        }
        else if(view is WatchView)
        {
            await ValidateContentUserRelatedView<WatchView>((view as WatchView)!, existing as WatchView, requester, action);
        }
        else if(view is VoteView)
        {
            await ValidateContentUserRelatedView<VoteView>((view as VoteView)!, existing as VoteView, requester, action);
        }
        else if (view is BanView)
        {
            var bView = (view as BanView)!;
            var exView = existing as BanView;

            if (!requester.super)
                throw new ForbiddenException("Only supers can set bans!");
            
            //Go lookup the user, simply doing so should throw the not found exception
            var user = await searcher.GetById<UserView>(RequestType.user, bView.bannedUserId, true);

            if (user == null || user.type != UserType.user)
                throw new RequestException($"You can only ban users, not other user types!");
            
            if(action == UserAction.delete)
                throw new ForbiddenException("You cannot delete bans just yet, please simply modify existing bans!");
        }
        else if(view is UserVariableView)
        {
            var uvView = (view as UserVariableView)!;
            var exView = existing as UserVariableView;

            if(action == UserAction.create)
            {
                //Also, ensure we're not adding a second watch for the same content
                var dupeVariable = await searcher.SearchSingleTypeUnrestricted<T>(new SearchRequest()
                {
                    type = "uservariable",
                    fields = "*",
                    query = "userId = @me and key = @key"
                }, new Dictionary<string, object> {
                    { "me", requester.id },
                    { "key", uvView.key }
                });

                if(dupeVariable.Count > 0)
                    throw new RequestException($"Duplicate variable name {uvView.key}!");
            }
            else
            {
                if(exView?.userId != requester.id)
                    throw new ForbiddenException($"You can only modify your own variables!");
            }
        }
        else 
        {
            //Be SAFER than sorry! All views when created are by default NOT writable!
            throw new ForbiddenException($"View of type {view.GetType().Name} cannot be user-modified!");
        }
    }

    public async Task ValidateBanOnContent(long contentId, BanView? requesterBan)
    {
        if (requesterBan != null)
        {
            var isPublic = await IsItemPublic(contentId);

            if (isPublic && (requesterBan.type & BanType.@public) > 0 || !isPublic && (requesterBan.type & BanType.@private) > 0)
                throw new BannedException($"You are banned from posting on {(isPublic ? "public" : "private")} content until {requesterBan.expireDate}. Reason: {requesterBan.message}");
        }
    }

    /// <summary>
    /// Validation for most (all?) db objects that are user generated related to content operate with the 
    /// same rules: you can't add duplicates, the content must exist, you must have read permissions. When
    /// modifying or deleting, you can only do so to your own objects
    /// </summary>
    /// <param name="view"></param>
    /// <param name="existing"></param>
    /// <param name="requester"></param>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task ValidateContentUserRelatedView<T>(T view, T? existing, UserView requester, UserAction action) 
        where T : class, IContentUserRelatedView, new()
    {
        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException($"Tried to validate content related view {typeof(T)} but it had no requestType!");

        //Watch is fairly simple, because the fields do most of the work
        if(action == UserAction.create)
        {
            //Just by searching, you will get the exceptions I hope for...
            var content = await searcher.GetById<ContentView>(RequestType.content, view.contentId, true);

            if(content == null || !(await CanUserAsync(requester, UserAction.read, content.id)))
                throw new ForbiddenException($"You don't have access to content {view.contentId}");

            //Also, ensure we're not adding a second watch for the same content
            var dupeThing = await searcher.SearchSingleTypeUnrestricted<T>(new SearchRequest()
            {
                type = requestType.ToString(),
                fields = "*",
                query = "userId = @me and contentId = @cid"
            }, new Dictionary<string, object> {
                { "me", requester.id },
                { "cid", view.contentId }
            });

            if(dupeThing.Count > 0)
                throw new RequestException($"Duplicate {requestType} for content {view.contentId}");
        }
        else if(action == UserAction.update || action == UserAction.delete)
        {
            if(existing?.userId != requester.id)
                throw new ForbiddenException($"You can only modify your own {requestType}!");
        }
    }

    //Ensure the action and the view make sense for each other
    public void ValidateViewForAction(UserAction action, IIdView view)
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
    /// Inserts, updates, and deletes for any type should call THIS function
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public async Task<T> GenericWorkAsync<T>(T view, UserView requester, UserAction action, string? message) where T : class, IIdView, new()
    {
        ValidateViewForAction(action, view);

        long id = 0;
        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException($"Type {typeof(T)} has no associated request type for querying, don't know how to write! This usually means this type is a complex type that doesn't represent a database object, and thus the API has a configuration error!");

        //This will throw an exception if we can't find it by id, so that's the end of that...
        //ALSO we specifically ask to throw on deleted content, because we don't want ANYBODY to be touching that stuff
        //through this interface. Deletes require a special restore!
        T? existing = view.id == 0 ? null : await searcher.GetById<T>(requestType, view.id, true);
        //object? existingRaw = view.id == 0 ? null : await searcher.QueryRawAsync($"select * from {typeInfo.}")

        //It's more important to throw a NotFound exception than the permission exception, it keeps things more consistent, so
        //keep this call down here.
        await ValidateWorkGeneral(view, existing, requester, action);

        //Ok at this point, we know everything is good to go, there shouldn't be ANY MORE checks required past here!
        if(view is ContentView)
        {
            id = await DatabaseWork_Content(
                new DbWorkUnit<ContentView>((view as ContentView)!, requester, typeInfo, action, existing as ContentView, message));
        }
        else if(view is MessageView)
        {
            id = await DatabaseWork_Message(
                new DbWorkUnit<MessageView>((view as MessageView)!, requester, typeInfo, action, existing as MessageView, message));
        }
        else if (view is UserView)
        {
            id = await DatabaseWork_User(
                new DbWorkUnit<UserView>((view as UserView)!, requester, typeInfo, action, existing as UserView, message));
        }
        else if (view is WatchView)
        {
            id = await DatabaseWork_ContentUserRelated<WatchView, Db.ContentWatch>(
                new DbWorkUnit<WatchView>((view as WatchView)!, requester, typeInfo, action, existing as WatchView, message),
                EventType.watch_event);
        }
        else if (view is VoteView)
        {
            id = await DatabaseWork_ContentUserRelated<VoteView, Db.ContentVote>(
                new DbWorkUnit<VoteView>((view as VoteView)!, requester, typeInfo, action, existing as VoteView, message),
                EventType.none);
        }
        else if (view is UserVariableView)
        {
            id = await DatabaseWork_UserVariable(
                new DbWorkUnit<UserVariableView>((view as UserVariableView)!, requester, typeInfo, action, existing as UserVariableView, message));
        }
        else if (view is BanView)
        {
            id = await DatabaseWork_Ban(
                new DbWorkUnit<BanView>((view as BanView)!, requester, typeInfo, action, existing as BanView, message));
        }
        else
        {
            throw new ArgumentException($"Don't know how to write type {typeof(T).Name} to the database!");
        }

        //The regardless, just go look up whatever work we just did, give the user the MOST up to date information, even if it's inefficient
        if(action == UserAction.delete && TrueDeletes.Contains(requestType))
            return view;
        else
            return await searcher.GetById<T>(requestType, id);
    }

    /// <summary>
    /// Use work unit given to translate as much as possible, including auto-generated data or preserved data. As such, we need the actual work item //all standard mapped fields from view to model.
    /// </summary>
    /// <param name="work"></param>
    /// <param name="dbModel"></param>
    /// <returns>Fields that were NOT mapped</returns>
    public List<string> MapSimpleViewFields<T>(DbWorkUnit<T> work, object dbModel) where T : class, IIdView, new()
    {
        //This can happen if our view type has no associated table
        if(work.typeInfo.writeAsInfo == null) 
            throw new InvalidOperationException($"Typeinfo for type {work.typeInfo.type} doesn't appear to have an associated db model!");
    
        //Assume all properties will be mapped
        var unmapped = new List<string>(); 

        //We want to get as many fields as possible. If there are some we can't map, that's ok.
        foreach(var dbModelProp in work.typeInfo.writeAsInfo.modelProperties) 
        {
            //Simply go find the field definition where the real database column is the same as the model property. 
            var remap = work.typeInfo.fields.FirstOrDefault(x => x.Value.fieldSelect == dbModelProp.Key);

            var isWriteRuleSet = new Func<WriteRule, bool>(t => 
                remap.Value.onInsert == t && work.action == UserAction.create ||
                remap.Value.onUpdate == t && work.action == UserAction.update
            );

            //Nothing was found for this model property, that's not good!
            if(string.IsNullOrWhiteSpace(remap.Key))
            {
                unmapped.Add(dbModelProp.Key);
                continue;
            }

            //OK, there's a lot going on with model properties. Basically, attributes allow us to automatically set fields on
            //models for insert and update, which we check for below. If none of the special checks work, we default to 
            //allowing whatever the user set.
            if(isWriteRuleSet(WriteRule.AutoDate)) 
            {
                dbModelProp.Value.SetValue(dbModel, DateTime.UtcNow);
            }
            else if(isWriteRuleSet(WriteRule.AutoUserId)) 
            {
                dbModelProp.Value.SetValue(dbModel, work.requester.id);
            }
            else if(isWriteRuleSet(WriteRule.Increment)) 
            {
                if(remap.Value.fieldType != typeof(int))
                    throw new InvalidOperationException($"API ERROR: tried to auto-increment non-integer field {remap.Key} (type: {remap.Value.fieldType})");

                int original = (int)(remap.Value.rawProperty?.GetValue(work.existing) ?? 0);
                dbModelProp.Value.SetValue(dbModel, original + 1);
            }
            else if(isWriteRuleSet(WriteRule.Preserve)) 
            {
                //Preserve for create means default value. This might change if it's confusing
                if(work.action == UserAction.create)
                {
                    if (remap.Value.fieldType.IsValueType)
                        dbModelProp.Value.SetValue(dbModel, Activator.CreateInstance(remap.Value.fieldType));
                    else
                        dbModelProp.Value.SetValue(dbModel, null);
                }
                else
                {
                    dbModelProp.Value.SetValue(dbModel, remap.Value.rawProperty?.GetValue(work.existing));
                }
            }
            else
            {
                //Set the dbmodel property value to be the view's property. Type matching is NOT checked, please be careful!
                dbModelProp.Value.SetValue(dbModel, remap.Value.rawProperty?.GetValue(work.view)); 
            }
        }

        return unmapped;
    }

    /// <summary>
    /// Delete stuff of the given type associated with the given anything. WARNING: THIS WORKS FOR COMMENTS, VOTES, WATCHES, ETC!!
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tsx"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public async Task DeleteAssociatedType<T>(long id, IDbTransaction tsx, string field, string type)
    {
        var tinfo = typeInfoService.GetTypeInfo<T>();
        var parameters = new { id = id };
        var sql = $"delete from {tinfo.selfDbInfo?.modelTable} where {field} = @id";
        logger.LogInformation($"Running DELETE sql (associated type {tinfo.type}): {sql}");
        var deleteCount = await dbcon.ExecuteAsync(sql, parameters, tsx);
        logger.LogInformation($"Deleted {deleteCount} {typeof(T).Name} from {type} {id}");
    }


    /// <summary>
    /// Delete stuff of the given type associated with the given content. WARNING: THIS WORKS FOR COMMENTS, VOTES, WATCHES, ETC!!
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tsx"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Task DeleteContentAssociatedType<T>(long id, IDbTransaction tsx)
    {
        return DeleteAssociatedType<T>(id, tsx, "contentId", "content");
    }

    /// <summary>
    /// Delete the values, keywords, and permissions associted with the given content. Note that these are the things
    /// which should be reset on content update.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="tsx"></param>
    /// <returns></returns>
    public async Task DeleteContentAssociatedAll(long id, IDbTransaction tsx)
    {
        await DeleteContentAssociatedType<ContentValue>(id, tsx);
        await DeleteContentAssociatedType<ContentKeyword>(id, tsx);
        await DeleteContentAssociatedType<ContentPermission>(id, tsx);
    }

    public AdminLog MakeContentLog(UserView requester, ContentView view, UserAction action)
    {
        return new AdminLog {
            text = $"User '{requester.username}'({requester.id}) {action}d {view.contentType} '{view.name}'({view.id})",
            type = action == UserAction.create ? AdminLogType.content_create :  
                    action == UserAction.update ? AdminLogType.content_update :
                    action == UserAction.delete ? AdminLogType.content_delete :
                    throw new InvalidOperationException($"Unsupported admin log type {action}"),
            target = view.id,
            initiator = requester.id,
            createDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// This function handles any database modification for the given view, from inserts to updates to deletes.
    /// NOTE: these work functions should NOT perform validation! That's all done beforehand!
    /// </summary>
    /// <param name="view"></param>
    /// <param name="requester"></param>
    /// <param name="typeInfo"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public async Task<long> DatabaseWork_Content(DbWorkUnit<ContentView> work)
    {
        //NOTE: other validation is performed in the generic, early validation

        var content = new Db.Content();
        var unmapped = MapSimpleViewFields(work, content);

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in content!");

        //And the final modification to content just before storing. NORMALLY we'd want to do this in the "Tweak" function but
        //content is special and some fields are inaccessible in the base view form.
        if(work.action == UserAction.delete)
        {
            //These are special and will be handled later anyway
            work.view.keywords.Clear();
            work.view.values.Clear();
            work.view.permissions.Clear();

            //This needs to be here instead of tweak because the views have different fields all mapped to content.
            //Content is special, as usual
            content.createUserId = 0;
            content.text = "";
            content.name = "deleted_content";
            content.deleted = true;
            content.description = null;
            content.literalType = null;
            content.meta = null;
            //Don't give out any information
            content.contentType = InternalContentType.none;
        }
        else if(work.action == UserAction.create) {
            work.view.permissions[work.requester.id] = "CRUD"; //FORCE permissions to include full access for creator all the time
        }
        else if(work.action == UserAction.update) {
            work.view.permissions[work.existing!.createUserId] = "CRUD"; //FORCE permissions to include full access for creator all the time
        }

        //Very particular checks. Some fields are marked as writable, but some types don't allow that
        if(work.action == UserAction.update && work.view.contentType == InternalContentType.file)
            content.literalType = work.existing?.literalType;
        

        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
            {
                //This is VERY silly, but the actual write needs to within a hash lock! This is the only
                //time we need to worry about the hash!
                if(string.IsNullOrWhiteSpace(content.hash))
                {
                    //Autogenerate
                    await GenerateContentHash(async x => {
                        content.hash = x;
                        work.view.id = await dbcon.InsertAsync(content, tsx);
                    });
                }
                else
                {
                    await VerifyHash(content.hash);
                    work.view.id = await dbcon.InsertAsync(content, tsx);
                }
            }
            else if(work.action == UserAction.update || work.action == UserAction.delete)
            {
                //Remove the old associated values
                await DeleteContentAssociatedAll(content.id, tsx);
                await dbcon.UpdateAsync(content, tsx);
            }
            else 
            {
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_Content!");
            }
            
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
            history.message = work.message;

            var activityId = await dbcon.InsertAsync(history, tsx);

            var adminLog = MakeContentLog(work.requester, work.view, work.action);
            await dbcon.InsertAsync(adminLog, tsx);

            tsx.Commit();

            //Content events are reported as activity
            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.activity_event, activityId));

            logger.LogDebug(adminLog.text); //The admin log actually has the log text we want!

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    /// <summary>
    /// A simple method to restore whatever content the given revision points to
    /// </summary>
    /// <param name="revision"></param>
    /// <param name="requestUserId"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public async Task<ContentView> RestoreContent(long revision, long requestUserId, string? message = null)
    {
        var user = await GetRequestUser(requestUserId);

        //Go find the revision and figure out the original content
        var htype = typeInfoService.GetTypeInfo<ContentHistory>();
        var revisions = await dbcon.QueryAsync<ContentHistory>($"select * from {htype.selfDbInfo!.modelTable} where {nameof(ContentHistory.id)} = @id", new {id = revision});
        var history = revisions.FirstOrDefault(x => x.id == revision);

        if(history == null)
            throw new NotFoundException($"Couldn't find revision {revision}!");

        if(!(await CanUserAsync(user, UserAction.update, history.contentId)))
            throw new ForbiddenException($"You're not allowed to update content {history.contentId}!");

        var maxRevision = await dbcon.QuerySingleAsync<long>($"select max({nameof(ContentHistory.id)}) from {htype.selfDbInfo!.modelTable} where {nameof(ContentHistory.contentId)} = @id", new {id = history.contentId});

        if(maxRevision == revision)
            throw new RequestException("You can't restore content to the revision it's already at!");
        
        var restoredContent = await historyConverter.HistoryToContentAsync(history);

        //OK, at this point we have all the stuff we need to update. We just clear out the old values, update the content, etc
        using(var tsx = dbcon.BeginTransaction())
        {
            //Remove the old associated values, update the content
            await DeleteContentAssociatedAll(restoredContent.id, tsx);
            await dbcon.UpdateAsync((Content)restoredContent, tsx);

            //insert the old values/permissions etc. These reuse the OLD ids from before, but should be fine!
            await dbcon.InsertAsync(restoredContent.values, tsx);
            await dbcon.InsertAsync(restoredContent.permissions, tsx);
            await dbcon.InsertAsync(restoredContent.keywords, tsx);

            //Still need to produce the update event
            var activity = await historyConverter.ContentToHistoryAsync(restoredContent, requestUserId, UserAction.update);
            activity.message = message;

            var activityId = await dbcon.InsertAsync(activity, tsx);

            var adminLog = new AdminLog()
            {
                text = $"User '{user.username}'({user.id}) restored {restoredContent.contentType} '{restoredContent.name}'({restoredContent.id}) from revision {revision}, was {maxRevision}",
                type = AdminLogType.content_restore,
                target = restoredContent.id,
                initiator = requestUserId,
                createDate = DateTime.UtcNow
            };
            await dbcon.InsertAsync(adminLog, tsx);

            tsx.Commit();

            //Content events are reported as activity
            await eventQueue.AddEventAsync(new LiveEvent(requestUserId, UserAction.update, EventType.activity_event, activityId));

            logger.LogDebug(adminLog.text); //The admin log actually has the log text we want!
        }

        return await searcher.GetById<ContentView>(RequestType.content, history.contentId);
    }
    
    public async Task<long> DatabaseWork_Message(DbWorkUnit<MessageView> work)
    {
        //NOTE: As usual, validation is performed outside this function!
        var comment = new Db.Message();
        var unmapped = MapSimpleViewFields(work, comment); 

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in comment!");

        //Clear out fields and junk on delete
        if(work.action == UserAction.delete)
        {
            work.view.values.Clear();
            comment.text = "deleted_comment";
            comment.createUserId = 0;
            comment.editUserId = 0;
            //comment.contentId = 0; //Specifically do NOT remove the parent! We need it for permissions!!
            comment.deleted = true;
        }

        //Need to update the edit history with the previous comment!
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
            {
                //This doesn't need a history, so write it as-is
                work.view.id = await dbcon.InsertAsync(comment, tsx);
            }
            else if(work.action == UserAction.update || work.action == UserAction.delete)
            {
                //Here, we need the snapshot of the PREVIOUS comment to add to the comment history!
                var snapshot = CreateSnapshotFromCommentWork(work);
                historyConverter.AddCommentHistory(snapshot, comment);
                await DeleteAssociatedType<MessageValue>(comment.id, tsx, nameof(MessageValue.messageId), "message");
                await dbcon.UpdateAsync(comment, tsx);
            }
            else 
            {
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_Message!");
            }

            //If we're not deleting, insert the various associated values from the comment
            if(work.action != UserAction.delete)
            {
                //These insert entire lists.
                await dbcon.InsertAsync(GetCommentValuesFromView(work.view), tsx);
            }

            tsx.Commit();

            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.message_event, work.view.id));

            logger.LogDebug($"User {work.requester.id} commented on {comment.contentId}"); //No admin log for comments, so have to construct the message ourselves

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    /// <summary>
    /// The database work for most (all?) user related objects for content are the same: a simple insert, update, or delete. We do
    /// a pure delete on all IContentUserRelatedViews. Also, unfortunately, dapper requires a solid type when calling Insert/etc,
    /// so even though the write type is described in an attribute on the view, you still need to pass the compile-time type here
    /// </summary>
    /// <typeparam name="long"></typeparam>
    public async Task<long> DatabaseWork_ContentUserRelated<T,V>(DbWorkUnit<T> work, EventType evType) 
        where T : class, IContentUserRelatedView, new()
        where V : class, new()
    {
        var type = work.typeInfo.requestType ?? throw new InvalidOperationException($"Tried to do database work for {typeof(T)} but it had no requestType!");
        //var dbType = work.typeInfo.writeAsInfo?.modelType ?? throw new InvalidOperationException($"Tried to do database work for {type} but it had no writeAsInfo!");

        //NOTE: As usual, validation is performed outside this function!
        var dbItem = Activator.CreateInstance<V>() ?? throw new InvalidOperationException($"Tried to create instance of db write object for {type} but it returned null!"); //new Db.ContentWatch();
        var unmapped = MapSimpleViewFields(work, dbItem); 

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in {type}!");

        //Need to update the edit history with the previous comment!
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            //This doesn't need a history, so write it as-is
            if(work.action == UserAction.create)
                work.view.id = await dbcon.InsertAsync(dbItem, tsx);
            else if(work.action == UserAction.update)
                await dbcon.UpdateAsync(dbItem, tsx);
            else if(work.action == UserAction.delete)
                await dbcon.DeleteAsync(dbItem, tsx);
            else 
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_ContentUserRelated!");

            tsx.Commit();

            if(evType != EventType.none)
            {
                //These are special: make sure you report the contentId since these types of things are purely deleted.
                await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, evType, work.view.id) {
                    contentId = work.view.contentId
                });
            }

            //No admin log for contentuserrelated, so have to construct the message ourselves
            var actionWord = work.action == UserAction.create ? "added" : work.action == UserAction.update ? "modified" : "deleted";
            logger.LogDebug($"User {work.requester.id} {actionWord} {type} for {work.view.contentId}"); 

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    public async Task<long> DatabaseWork_UserVariable(DbWorkUnit<UserVariableView> work)
    {
        //NOTE: As usual, validation is performed outside this function!
        var dbItem = new UserVariable();
        var unmapped = MapSimpleViewFields(work, dbItem); 

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in uservariable!");

        //Need to update the edit history with the previous comment!
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            //This doesn't need a history, so write it as-is
            if(work.action == UserAction.create)
                work.view.id = await dbcon.InsertAsync(dbItem, tsx);
            else if(work.action == UserAction.update)
                await dbcon.UpdateAsync(dbItem, tsx);
            else if(work.action == UserAction.delete)
                await dbcon.DeleteAsync(dbItem, tsx);
            else 
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_UserVariable!");

            tsx.Commit();

            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.uservariable_event, work.view.id));

            logger.LogDebug($"User {work.requester.id} did '{work.action}' on variable {work.view.key}"); 

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    public async Task<long> DatabaseWork_Ban(DbWorkUnit<BanView> work)
    {
        //NOTE: As usual, validation is performed outside this function!
        var dbItem = new Ban();
        var unmapped = MapSimpleViewFields(work, dbItem); 

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in ban!");

        //Need to update the edit history with the previous comment!
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            //The baseline admin log
            var adminLog = NewBaseLog(work); 

            //This doesn't need a history, so write it as-is
            if(work.action == UserAction.create)
            {
                work.view.id = await dbcon.InsertAsync(dbItem, tsx);
                adminLog.type = AdminLogType.ban_create;
            }
            else if(work.action == UserAction.update)
            {
                await dbcon.UpdateAsync(dbItem, tsx);
                adminLog.type = AdminLogType.ban_edit;
            }
            //else if(work.action == UserAction.delete)
            //    await dbcon.DeleteAsync(dbItem, tsx);
            else 
            {
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_Ban!");
            }

            adminLog.text = $"User {work.requester.username}({work.requester.id}) {work.action}d ban for user {work.view.bannedUserId} until {work.view.expireDate}, message: {work.view.message}";
            await dbcon.InsertAsync(adminLog, tsx);
            tsx.Commit();

            //No event reporting yet
            //await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.uservariable_event, work.view.id));

            logger.LogDebug($"User {work.requester.id} did '{work.action}' on ban for user {work.view.bannedUserId}: {work.view.message}"); 

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    /// <summary>
    /// This function handles any database modification for the given view, from inserts to updates to deletes.
    /// </summary>
    /// <param name="view"></param>
    /// <param name="requester"></param>
    /// <param name="typeInfo"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public async Task<long> DatabaseWork_User(DbWorkUnit<UserView> work)
    {
        var user = new Db.User();
        var unmapped = MapSimpleViewFields(work, user);

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in user!");

        if(work.action == UserAction.create)
        {
            //Ensure they can NEVER register, even if the user service somehow fails to catch it!
            user.registrationKey = $"INVALID_{Guid.NewGuid()}";
        }
        else if(work.action == UserAction.delete)
        {
            //These groups are added later anyway, don't worry about it being after map
            work.view.usersInGroup.Clear();
            user.avatar = "";               //This might need special attention!
            user.username = $"deleted_user_{work.view.id}"; //Want all usernames to be unique probably...
            user.password = "";
            user.registrationKey = $"INVALID_{Guid.NewGuid()}";
            user.salt = "";
            user.email = "";
            user.special = "";
            //user.hidelist = "";
            user.super = false;
            user.deleted = true;
        }
        else if(work.action == UserAction.update)
        {
            //Users are special: we have to go get the original so we can preserve some of the fields. We can be pretty sure a lookup by id will work because
            //we've had so much validation before we get here.
            var original = searcher.ToStronglyTyped<User>(await searcher.QueryRawAsync($"select * from users where id = @id", new Dictionary<string, object> {{"id", work.view.id}})).First();
            user.editDate = DateTime.UtcNow;
            //TODO: this whole thing is very bad, if you forget to come here and add the copy back for a field, it could spell disaster
            user.registrationKey = original.registrationKey;
            user.password = original.password;
            user.salt = original.salt;
            user.email = original.email;
            user.lastPasswordDate = original.lastPasswordDate;
        }

        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
            {
                work.view.id = await dbcon.InsertAsync(user, tsx);
            }
            else if(work.action == UserAction.update || work.action == UserAction.delete)
            {
                //Remove the old associated values
                await DeleteAssociatedType<UserRelation>(user.id, tsx, nameof(Db.UserRelation.relatedId), "user"); //For now, just remove where the user is directly referenced. We MIGHT need the relatedId, but what if that's content??
                await dbcon.UpdateAsync(user, tsx);
            }
            else 
            {
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_User!");
            }

            //Note: when deleting, don't want to write ANY extra data, regardless of what's there!
            if(work.action != UserAction.delete)
            {
                //Need to insert the group relations. We've already verified the groups, AND removed the old relations.
                await dbcon.InsertAsync(work.view.usersInGroup.Select(x => new UserRelation()
                {
                    type = UserRelationType.in_group,
                    userId = x, //work.view.id,
                    relatedId = work.view.id,
                    createDate = DateTime.UtcNow
                }), tsx);
            }

            await InsertAdminLogForUserWork(work, tsx);
            tsx.Commit();

            //User events are reported for the purpose of tracking 
            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.user_event, work.view.id));

            logger.LogDebug($"User {work.requester.id}({work.requester.username}) did action '{work.action}' on user {work.view.id}"); 

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    public async Task InsertAdminLogForUserWork(DbWorkUnit<UserView> work, IDbTransaction tsx)
    {
        //The baseline admin log
        var adminLog = new AdminLog()
        {
            createDate = DateTime.UtcNow,
            initiator = work.requester.id,
            target = work.view.id
        };

        //Write admin log only for specific circumstances, don't need to track everything users do
        if (work.action == UserAction.update && work.view.username != work.existing?.username)
        {
            adminLog.type = AdminLogType.username_change;
            adminLog.text = $"User '{work.requester.username}'({work.requester.id}) changed username for {work.existing?.username}({work.existing?.type}) to '{work.view.username}'";
            await dbcon.InsertAsync(adminLog, tsx);
        }
        if (work.action == UserAction.create && work.view.type == UserType.group)
        {
            adminLog.type = AdminLogType.group_create;
            adminLog.text = $"User '{work.requester.username}'({work.requester.id}) created group '{work.view.username}'({work.view.id}) with users '{string.Join(",", work.view.usersInGroup)}'";
            await dbcon.InsertAsync(adminLog, tsx);
        }
        if (work.action == UserAction.delete) // && work.view.type == UserType.group)
        {
            adminLog.type = AdminLogType.user_delete;
            adminLog.text = $"User '{work.requester.username}'({work.requester.id}) deleted {work.existing!.type} '{work.existing!.username}'({work.view.id})";
            await dbcon.InsertAsync(adminLog, tsx);
        }
        if (work.action == UserAction.update && work.view.type == UserType.group && !work.view.usersInGroup.OrderBy(x => x).SequenceEqual(work.existing!.usersInGroup.OrderBy(x => x)))
        {
            adminLog.type = AdminLogType.group_assign;
            adminLog.text = $"User '{work.requester.username}'({work.requester.id}) modified assigned users in group '{work.view.username}'({work.view.id}) from '{string.Join(",", work.existing!.usersInGroup)}' to '{string.Join(",", work.view.usersInGroup)}'";
            await dbcon.InsertAsync(adminLog, tsx);
        }
    }

    public async Task ValidateUsers(List<long> users, UserView requester)
    {
        //Nothing to check
        if(users.Count == 0)
            return;

        if(users.Distinct().Count() != users.Count)
            throw new ArgumentException("Duplicate users found in group user list");

        //Just go lookup the groups
        var utinfo = typeInfoService.GetTypeInfo<UserView>();
        var foundGroups = await searcher.QueryRawAsync($"select id,super from users where id in @ids and type = @type", new Dictionary<string, object> {
            { "ids", users },
            { "type", UserType.user }
        });

        foreach(var id in users)
        {
            var fg = foundGroups.FirstOrDefault(x => id.Equals(x["id"]));

            if(fg == null)
                throw new ArgumentException($"User {id} in user set not found in database!"); // Note: only groups can be added, not users!");
        }
    }

    public async Task ValidatePermissionFormat(Dictionary<long, string> permissions)
    {
        //Nothing to check
        if(permissions.Count == 0)
            return;

        if(permissions.Keys.Distinct().Count() != permissions.Keys.Count())
            throw new ArgumentException("Duplicate user(s) in permissions set!");

        //Look up all users, DON'T NEED all fields
        var utinfo = typeInfoService.GetTypeInfo<UserView>();
        var foundUsers = await searcher.QueryRawAsync($"select id from users where id in @ids", new Dictionary<string, object> { { "ids",  permissions.Keys } });
        var foundUserIds = foundUsers.Select(x => x["id"]);

        //Do the per-permission check now!
        foreach(var id in permissions.Keys)
        {
            if (id != 0 && !foundUserIds.Contains(id))
                throw new ArgumentException($"User {id} in permissions set not found in database! Are they a user?");

            //I don't care about uppercase or duplicates, fix that for the user, whatever
            var original = Regex.Replace(permissions[id], @"\s+", "");
            permissions[id] = string.Concat(original.ToUpper().Distinct());

            //BUT, the right values need to be in there!
            if(!Regex.IsMatch(permissions[id], @"^[CRUD]*$"))
                throw new ArgumentException($"Unrecognized permission for user {id} in string: {permissions[id]}");
        }
    }

    /// <summary>
    /// Create a ContentSnapshot using an existing content object, plus a view that presumably has the rest of the fields.
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
            value = JsonConvert.SerializeObject(x.Value),
            contentId = originalView.id
        }).ToList();

        snapshot.permissions = permissionService.PermissionsToDb(originalView);

        return snapshot;
    }

    /// <summary>
    /// Compute writable database comment values from the given view
    /// </summary>
    /// <param name="view"></param>
    /// <returns></returns>
    public List<MessageValue> GetCommentValuesFromView(MessageView view)
    {
        return view.values.Select(x => new Db.MessageValue {
            key = x.Key,
            value = JsonConvert.SerializeObject(x.Value),
            messageId = view.id
        }).ToList();
    }

    /// <summary>
    /// </summary>
    /// <param name="work"></param>
    /// <returns></returns>
    public CommentSnapshot CreateSnapshotFromCommentWork(DbWorkUnit<MessageView> work)
    {
        var realExisting = work.existing ?? throw new InvalidOperationException("NO EXISTING MESSAGEVIEW WHEN CREATING SNAPSHOT, INTERNAL ERROR!");
        return new CommentSnapshot {
            userId = work.requester.id,
            editDate = DateTime.UtcNow,
            previous = realExisting.text,
            contentId = realExisting.contentId,
            action = work.action,
            values = GetCommentValuesFromView(realExisting)
        };
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
        string checkCol = permissionService.ActionToColumn(action);

        //Just about as optimized as I can make it: a raw query that only returns a count,
        //AND the contentId search is from an index so... at most it'll be like log2(n) + C where
        //C is the amount of users defined in the content permission set, and N is the total amount
        //of pages. So if we had oh I don't know, 2 billion pages, it might take like 40 iterations.
        return (await dbcon.ExecuteScalarAsync<int>(@$"select count(*)
             from content_permissions
             where {nameof(ContentPermission.contentId)} = @contentId
               and {nameof(ContentPermission.userId)} in @requesters 
               and `{checkCol}` = 1
            ", new { contentId = thing, requesters = permissionService.GetPermissionIdsForUser(requester) })) > 0;
    }

    public Task<bool> IsItemPublic(long thing)
    {
        return CanUserAsync(new UserView() { id = 0, super = false, groups = new List<long>() }, UserAction.read, thing);
    }

    public async Task<string> GenerateContentHash(Func<string, Task> writeHash)
    {
        await hashLock.WaitAsync();

        try
        {
            var hash = "";
            int retries = 0;
            while (true)
            {
                hash = rng.GetAlphaSequence(config.AutoHashChars);

                //Not a duplicate hash? We can quit!
                if(!(await IsDuplicateHash(hash)))
                    break;
                else
                    logger.LogWarning($"DUPLICATE HASH: {hash}");

                retries++;

                if (retries > config.AutoHashMaxRetries)
                    throw new InvalidOperationException("Ran out of hash retries! Maybe there's too many files on the system?");
            }

            //WARN: if this doesn't return for some reason, the hashLock will never release! We'll see how this works...
            await writeHash(hash);
            return hash;
        }
        finally
        {
            hashLock.Release();
        }
    }

    public async Task<bool> IsDuplicateHash(string hash)
    {
        return (await searcher.GetByField<ContentView>(RequestType.content, "hash", hash)).Count != 0;
    }

    public async Task VerifyHash(string hash)
    {
        if(hash.Length > config.HashMaxLength)
            throw new ArgumentException($"Hash '{hash}' too long! Max: {config.HashMaxLength}");
        else if(hash.Length < config.HashMinLength)
            throw new ArgumentException($"Hash '{hash}' too short! Min: {config.HashMinLength}");
        else if(!Regex.IsMatch(hash, config.HashRegex))
            throw new ArgumentException($"Hash '{hash}' has invalid characters!");

        if(await IsDuplicateHash(hash))
            throw new ArgumentException($"Duplicate hash: already content with hash '{hash}'!");
    }

    public AdminLog NewBaseLog<T>(DbWorkUnit<T> work, AdminLogType type = AdminLogType.none) where T : class, IIdView, new()
    {
        return new AdminLog()
        {
            id = 0,
            createDate = DateTime.UtcNow,
            initiator = work.requester.id,
            target = work.view.id,
            type = type
        };
    }

    public async Task<AdminLog> WriteAdminLog(AdminLog log)
    {
        log.id = 0;
        log.createDate = DateTime.UtcNow;
        //Probably not necessary to reassign
        log.id = await dbcon.InsertAsync(log);
        return log;
    }
}