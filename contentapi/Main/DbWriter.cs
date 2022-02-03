using System.Data;
using System.Text.RegularExpressions;
using AutoMapper;
using contentapi.Db;
using contentapi.Db.History;
using contentapi.Live;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.Main;

public class DbWriter : IDbWriter
{
    protected ILogger logger;
    protected IGenericSearch searcher;
    protected IDbConnection dbcon;
    protected IDbTypeInfoService typeInfoService;
    protected IMapper mapper;
    protected IHistoryConverter historyConverter;
    protected IPermissionService permissionService;
    protected ILiveEventQueue eventQueue;

    public DbWriter(ILogger<DbWriter> logger, IGenericSearch searcher, ContentApiDbConnection connection,
        IDbTypeInfoService typeInfoService, IMapper mapper, IHistoryConverter historyConverter,
        IPermissionService permissionService, ILiveEventQueue eventQueue)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.dbcon = connection.Connection;
        this.typeInfoService = typeInfoService;
        this.mapper = mapper;
        this.historyConverter = historyConverter;
        this.permissionService = permissionService;
        this.eventQueue = eventQueue;
    
        //Preemptively open this, we know us (as a writer) SHOULD BE short-lived, so...
        this.dbcon.Open();
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
        return await GenericWorkAsync(new T() { id = id }, await GetRequestUser(requestUserId), UserAction.delete, message);
    }

    /// <summary>
    /// Throws "ForbiddenException" for any kind of permission error that could arise for a modification given. ALSO perform other
    /// validation in here!
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public async Task ValidateUserPermissionForAction<T>(T view, T? existing, UserView requester, UserAction action) where T : class, IIdView, new()
    {
        //Each type needs a different kind of check
        if(view is ContentView)
        {
            var cView = view as ContentView ?? throw new InvalidOperationException("Somehow, ContentView could not be cast to a ContentView");

            //Only need to check parent for create if it's actually a place! This is because we treat non-valid 
            //ids as orphaned pages
            if((action == UserAction.create || action == UserAction.update) && (cView.parentId > 0))
            {
                if(!(await CanUserAsync(requester, action, cView.parentId)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' content in parent {cView.parentId}!");
            }

            //Create is special, because we only check the parent for create (and we did that earlier)
            if(action == UserAction.create)
            {
                //Only supers can create modules, but after that, permissions are based on their internal permissions.
                if(view is ModuleView && !requester.super)
                    throw new ForbiddenException($"Only supers can create modules!");
            }
            else if(action != UserAction.read)
            {
                //We generally assume that the views and such have proper fields by the time it gets to us...
                if(!(await CanUserAsync(requester, action, cView.id)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' content {cView.id}!");
            }

            //Now for general validation
            await ValidatePermissionFormat(cView.permissions);

            if(cView.deleted)
                throw new RequestException("Don't delete content by setting the deleted flag!");
        }
        else if(view is CommentView)
        {
            var cView = view as CommentView ?? throw new InvalidOperationException("Somehow, CommentView could not be cast to a CommentView");

            //Modification actions
            if(action == UserAction.update || action == UserAction.delete)
            {
                var exView = existing as CommentView ?? throw new InvalidOperationException("Permissions wasn't given the old view during check!");

                if(!(requester.super || exView.createUserId == requester.id))
                    throw new ForbiddenException($"Only the original poster and supers can modify existing comments!");
            }

            //Create is special, because we need the parent create permission. We also check updates so users can't 
            //move a comment into an unusable room (if that ever gets allowed)
            if(action == UserAction.create || action == UserAction.update)
            {
                //No orphaned comments, so the parent MUST exist! This is an easy check. You will get a "notfound" exception
                var parent = await searcher.GetById<ContentView>(RequestType.content, cView.contentId, true);

                //Can't post in invalid locations! So we check ANY contentId passed in, even if it's invalid
                if(!(await CanUserAsync(requester, action, cView.contentId)))
                    throw new ForbiddenException($"User {requester.id} can't '{action}' comments in content {cView.contentId}!");
            }

            if(cView.deleted)
                throw new RequestException("Don't delete comments by setting the deleted flag!");
        }
        else if(view is UserView)
        {
            var uView = (view as UserView)!;
            var exView = existing as UserView;
            
            //NOTE: adding groups to groups is supported but does not do what users expect it to do: you cannot create nested groups!
            await ValidateGroups(uView.groups, requester);

            //Users aren't created here except by supers? This might change sometime
            if(action == UserAction.create)
            {
                if(!requester.super)
                    throw new ForbiddenException("You can't create users or groups through this endpoint unless you're a super user!");
            }

            if(action == UserAction.update)
            {
                if(requester.id != uView.id && !requester.super)
                    throw new ForbiddenException("You cannot modify users other than yourself unless you're a super user!");

                if(uView.super != exView?.super && !requester.super)
                    throw new ForbiddenException("You cannot modify the super state unless you're a super user!");

                //if(!uView.groups.OrderBy(x => x).SequenceEqual() && !requester.super)
                //    throw new ForbiddenException("You cannot modify your own groups unless you're a super user!");
            }

            if(action == UserAction.delete)
            {
                if(!requester.super)
                    throw new ForbiddenException("Only super users can delete users!");
            }
        }
        else 
        {
            //Be SAFER than sorry! All views when created are by default NOT writable!
            throw new ForbiddenException($"View of type {view.GetType().Name} cannot be user-modified!");
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
        await ValidateUserPermissionForAction(view, existing, requester, action);

        //Ok at this point, we know everything is good to go, there shouldn't be ANY MORE checks required past here!
        if(view is ContentView)
        {
            id = await DatabaseWork_Content(new DbWorkUnit<ContentView>((view as ContentView)!, requester, typeInfo, action, existing as ContentView, message));
        }
        else if(view is CommentView)
        {
            id = await DatabaseWork_Comments(new DbWorkUnit<CommentView>((view as CommentView)!, requester, typeInfo, action, existing as CommentView, message));
        }
        else if (view is UserView)
        {
            id = await DatabaseWork_User(new DbWorkUnit<UserView>((view as UserView)!, requester, typeInfo, action, existing as UserView, message));
        }
        else
        {
            throw new ArgumentException($"Don't know how to write type {typeof(T).Name} to the database!");
        }

        //The regardless, just go look up whatever work we just did, give the user the MOST up to date information, even if it's inefficient
        return await searcher.GetById<T>(requestType, id);
    }

    /// <summary>
    /// Use TypeInfo given to translate as much as possible, including auto-generated data or preserved data. As such, we need the actual work item //all standard mapped fields from view to model.
    /// </summary>
    /// <param name="tinfo"></param>
    /// <param name="view"></param>
    /// <param name="dbModel"></param>
    /// <returns>Fields that were NOT mapped</returns>
    public List<string> MapSimpleViewFields<T>(DbWorkUnit<T> work, object dbModel) where T : class, IIdView, new()
    {
        //This can happen if our view type has no associated table
        if(work.typeInfo.modelType == null) 
            throw new InvalidOperationException($"Typeinfo for type {work.typeInfo.type} doesn't appear to have an associated db model!");
    
        //Assume all properties will be mapped
        var unmapped = new List<string>(); 

        //We want to get as many fields as possible. If there are some we can't map, that's ok.
        foreach(var dbModelProp in work.typeInfo.modelProperties) 
        {
            //Simply go find the field definition where the real database column is the same as the model property. 
            var remap = work.typeInfo.fields.FirstOrDefault(x => x.Value.realDbColumn == dbModelProp.Key);

            var isWriteRuleSet = new Func<WriteRuleType, bool>(t => 
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
            if(isWriteRuleSet(WriteRuleType.AutoDate)) 
            {
                dbModelProp.Value.SetValue(dbModel, DateTime.UtcNow);
            }
            else if(isWriteRuleSet(WriteRuleType.AutoUserId)) 
            {
                dbModelProp.Value.SetValue(dbModel, work.requester.id);
            }
            else if(isWriteRuleSet(WriteRuleType.Increment)) 
            {
                if(remap.Value.fieldType != typeof(int))
                    throw new InvalidOperationException($"API ERROR: tried to auto-increment non-integer field {remap.Key} (type: {remap.Value.fieldType})");

                int original = (int)(remap.Value.rawProperty?.GetValue(work.existing) ?? throw new InvalidOperationException("Integer field was somehow null!!"));
                dbModelProp.Value.SetValue(dbModel, original + 1);
            }
            else if(isWriteRuleSet(WriteRuleType.DefaultValue)) 
            {
                if(remap.Value.fieldType.IsValueType)
                    dbModelProp.Value.SetValue(dbModel, Activator.CreateInstance(remap.Value.fieldType));
                else
                    dbModelProp.Value.SetValue(dbModel, null);
            }
            else if(isWriteRuleSet(WriteRuleType.Preserve)) 
            {
                dbModelProp.Value.SetValue(dbModel, remap.Value.rawProperty?.GetValue(work.existing));
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
        var deleteCount = await dbcon.ExecuteAsync($"delete from {tinfo.modelTable} where {field} = @id", parameters, tsx);
        logger.LogInformation($"Deleting {deleteCount} {typeof(T).Name} from {type} {id}");
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
            text = $"User '{requester.username}'({requester.id}) {action}d '{view.name}'({view.id})",
            type = action == UserAction.create ? AdminLogType.contentCreate :  
                    action == UserAction.update ? AdminLogType.contentUpdate :
                    action == UserAction.delete ? AdminLogType.contentDelete :
                    throw new InvalidOperationException($"Unsupported admin log type {action}"),
            target = view.id,
            initiator = requester.id,
            createDate = DateTime.UtcNow
        };
    }

    public InternalContentType InternalContentTypeFromView(ContentView view)
    {
        //Don't forget to set the type appropriately!
        if(view is PageView)
            return InternalContentType.page;
        else if(view is FileView)
            return InternalContentType.file;
        else if(view is ModuleView)
            return  InternalContentType.module;
        else
            return InternalContentType.none;
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
        //Some basic sanity checks
        if(!work.typeInfo.type.IsAssignableTo(typeof(ContentView)))
            throw new InvalidOperationException($"TypeInfo given in DatabaseWork was for type '{work.typeInfo.type}', not '{typeof(ContentView)}'");
        if(work.action == UserAction.delete && work.typeInfo.type != typeof(ContentView))
            throw new InvalidOperationException("You must use the basetype ContentView when deleting content!");
        if((work.action == UserAction.create || work.action == UserAction.update) && work.typeInfo.type == typeof(ContentView))
            throw new InvalidOperationException("You cannot write the raw type ContentView! It doesn't have enough information!");

        //NOTE: other validation is performed in the generic, early validation

        var content = new Db.Content();
        var unmapped = MapSimpleViewFields(work, content); //.typeInfo, work.view, content);

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
            content.content = "";
            content.name = "deleted_content";
            content.deleted = true;
            content.extra1 = null;
            //Don't give out any information
            content.internalType = InternalContentType.none;
            content.publicType = "";
        }
        else
        {
            work.view.permissions[work.requester.id] = "CRUD"; //FORCE permissions to include full access for creator all the time
            content.internalType = InternalContentTypeFromView(work.view);
        }

        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
            {
                work.view.id = await dbcon.InsertAsync(content, tsx);
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
            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.activity, activityId));

            logger.LogDebug(adminLog.text); //The admin log actually has the log text we want!

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }
    
    public async Task<long> DatabaseWork_Comments(DbWorkUnit<CommentView> work)
    {
        if(!work.typeInfo.type.IsAssignableTo(typeof(CommentView)))
            throw new InvalidOperationException($"TypeInfo given in DatabaseWork was for type '{work.typeInfo.type}', not '{typeof(CommentView)}'");

        //NOTE: As usual, validation is performed outside this function!
        var comment = new Db.Comment();
        var unmapped = MapSimpleViewFields(work, comment); 

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in comment!");

        //Always ensure these fields are like this for comments
        comment.module = null;
        comment.receiveUserId = 0;

        //Append the history to the comment if we're modifying the comment instead of inserting a new one!
        if(work.action == UserAction.update || work.action == UserAction.delete)
        {
            if(work.action == UserAction.delete)
            {
                comment.text = "deleted_comment";
                comment.createUserId = 0;
                comment.editUserId = 0;
                comment.contentId = 0;
                comment.deleted = true;
            }

            historyConverter.AddCommentHistory(new CommentSnapshot {
                userId = work.requester.id,
                editDate = DateTime.UtcNow,
                previous = work.existing?.text,
                action = work.action
            }, comment);
        }

        //Need to update the edit history with the previous comment!
        //Always need an ID to link to, so we actually need to create the content first and get the ID.
        using(var tsx = dbcon.BeginTransaction())
        {
            if(work.action == UserAction.create)
                work.view.id = await dbcon.InsertAsync(comment, tsx);
            else if(work.action == UserAction.update || work.action == UserAction.delete)
                await dbcon.UpdateAsync(comment, tsx);
            else 
                throw new InvalidOperationException($"Can't perform action {work.action} in DatabaseWork_Comments!");

            tsx.Commit();

            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.comment, work.view.id));

            logger.LogDebug($"User {work.requester.id} commented on {comment.contentId}"); //No admin log for comments, so have to construct the message ourselves

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
        //Some basic sanity checks
        if(!work.typeInfo.type.IsAssignableTo(typeof(UserView)))
            throw new InvalidOperationException($"TypeInfo given in DatabaseWork was for type '{work.typeInfo.type}', not '{typeof(UserView)}'");

        var user = new Db.User();
        var unmapped = MapSimpleViewFields(work, user);

        if(unmapped.Count > 0)
            logger.LogWarning($"Fields '{string.Join(",", unmapped)}' not mapped in user!");


        if(work.action == UserAction.delete)
        {
            //These groups are added later anyway, don't worry about it being after map
            work.view.groups.Clear();
            user.avatar = "";               //This might need special attention!
            user.username = $"deleted_user_{work.view.id}"; //Want all usernames to be unique probably...
            user.password = "";
            user.registrationKey = "";
            user.salt = "";
            user.email = "";
            user.special = "";
            user.hidelist = "";
            user.super = false;
        }
        else if(work.action == UserAction.update)
        {
            //Users are special: we have to go get the original so we can preserve some of the fields. We can be pretty sure a lookup by id will work because
            //we've had so much validation before we get here.
            var original = searcher.ToStronglyTyped<User>(await searcher.QueryRawAsync($"select * from {work.typeInfo.modelTable} where id = @id", new Dictionary<string, object> {{"id", work.view.id}})).First();
            user.registrationKey = original.registrationKey;
            user.hidelist = original.hidelist;
            user.password = original.password;
            user.salt = original.salt;
            user.editDate = DateTime.UtcNow;
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
                await DeleteAssociatedType<UserRelation>(user.id, tsx, "userId", "user"); //For now, just remove where the user is directly referenced. We MIGHT need the relatedId, but what if that's content??
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
                await dbcon.InsertAsync(work.view.groups.Select(x => new UserRelation()
                {
                    type = UserRelationType.inGroup,
                    userId = work.view.id,
                    relatedId = x,
                    createDate = DateTime.UtcNow
                }), tsx);
            }

            //Write admin log only for specific circumstances, don't need to track everything users do
            if(work.action == UserAction.update && work.view.username != work.existing?.username)
            {
                await dbcon.InsertAsync(new AdminLog() {
                    type = AdminLogType.usernameChange,
                    createDate = DateTime.UtcNow,
                    initiator = work.requester.id,
                    target = work.view.id,
                    text = $"User {work.requester.id}({work.requester.username}) changed username for {work.existing?.username} to '{work.view.username}'"
                }, tsx);
            }

            tsx.Commit();

            //User events are reported for the purpose of tracking 
            await eventQueue.AddEventAsync(new LiveEvent(work.requester.id, work.action, EventType.user, work.view.id));

            logger.LogDebug($"User {work.requester.id}({work.requester.username}) did action '{work.action}' on user {work.view.id}"); 

            //NOTE: this is the newly computed id, we place it inside the view for safekeeping 
            return work.view.id;
        }
    }

    public async Task ValidateGroups(List<long> groups, UserView requester)
    {
        //Nothing to check
        if(groups.Count == 0)
            return;

        if(groups.Distinct().Count() != groups.Count)
            throw new ArgumentException("Duplicate groups found in user group list");

        //Just go lookup the groups
        var utinfo = typeInfoService.GetTypeInfo<UserView>();
        var foundGroups = await searcher.QueryRawAsync($"select id,super from {utinfo.modelTable} where id in @ids and type = @type", new Dictionary<string, object> {
            { "ids", groups },
            { "type", UserType.group }
        });
        //var foundGroupIds = foundGroups.Select(x => x["id"]);

        foreach(var id in groups)
        {
            var fg = foundGroups.FirstOrDefault(x => id.Equals(x["id"]));

            if(fg == null)
                throw new ArgumentException($"Group {id} in user group set not found in database! Note: only groups can be added, not users!");
            else if((long)fg["super"] > 0 && !requester.super)
                throw new ForbiddenException($"You can't put yourself in restricted group {id}!");
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
        var foundUsers = await searcher.QueryRawAsync($"select id from {utinfo.modelTable} where id in @ids", new Dictionary<string, object> { { "ids",  permissions.Keys } });
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

        snapshot.permissions = permissionService.PermissionsToDb(originalView);

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
             from {typeInfo.modelTable} 
             where {nameof(ContentPermission.contentId)} = @contentId
               and {nameof(ContentPermission.userId)} in @requesters 
               and `{checkCol}` = 1
            ", new { contentId = thing, requesters = permissionService.GetPermissionIdsForUser(requester) })) > 0;
    }
}