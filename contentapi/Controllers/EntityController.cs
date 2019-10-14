using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using contentapi.Models;
using contentapi.Services;

namespace contentapi.Controllers
{
    public class ActionCarryingException : Exception
    {
        public ActionResult Result;

        public ActionCarryingException() : base() { }
        public ActionCarryingException(string message) : base(message) {}
        public ActionCarryingException(string message, Exception inner) : base(message, inner) {}
    }

    //Too much work to manage the list of services for all these derived classes
    public class GenericControllerServices
    {
        public ContentDbContext context;
        public IMapper mapper;
        public PermissionService permission;
        public QueryService query;
        public ISessionService session;
        public IEmailService email;
        public ILanguageService language;
        public IHashService hash;
        public AccessService access;
        public IEntityService entity;

        public GenericControllerServices(ContentDbContext context, IMapper mapper, 
            PermissionService permissionService, QueryService queryService,
            ISessionService sessionService, IEmailService emailService, 
            ILanguageService languageService, IHashService hashService,
            AccessService accessService, IEntityService entityService)
        {
            this.context = context;
            this.mapper = mapper;
            this.permission = permissionService;
            this.query = queryService;
            this.session = sessionService;
            this.email = emailService;
            this.language = languageService;
            this.hash = hashService;
            this.access = accessService;
            this.entity = entityService;
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public abstract class EntityController<T,V> : ControllerBase where T : EntityChild where V : EntityView
    {
        protected GenericControllerServices services;

        protected bool DoActionLog = true;

        public EntityController(GenericControllerServices services)
        {
            this.services = services;
            services.session.Context = this; //EEEWWWW
        }

        // *************
        // * UTILITIES *
        // *************
        protected void ThrowAction(ActionResult result, string message = null)
        {
            if(message != null)
                throw new ActionCarryingException(message) {Result = result};
            else
                throw new ActionCarryingException() {Result = result};
        }

        protected async Task LogAct(EntityAction action, long entityId)
        {
            var log = new EntityLog()
            {
                action = action,
                createDate = DateTime.Now,
                entityId = entityId,
                userId = services.session.GetCurrentUid()
            };

            if(log.userId < 0)
                throw new InvalidOperationException("Cannot log actions for anonymous users!");

            await services.context.EntityLogs.AddAsync(log);
            await services.context.SaveChangesAsync();
        }

        public async Task<User> GetCurrentUserAsync()
        {
            return await services.context.Users.FindAsync(services.session.GetCurrentUid());
        }

        public async Task<bool> CanUserAsync(Permission permission)
        {
            var user = await GetCurrentUserAsync();

            if(user == null)
                return false;

            return services.permission.CanDo(user.role, permission);
        }

        //protected async Task<T> GetExisting(long id)
        //{
        //    try
        //    {
        //        return await services.context.GetSingleAsync<T>(id);
        //    }
        //    catch
        //    {
        //        ThrowAction(NotFound(id));
        //        return null; //just to satisfy the compiler
        //    }
        //}

        //How to RETURN items (the object we return... maybe make it a real class)
        public Dictionary<string, object> GetGenericCollectionResult<W>(IEnumerable<W> items, IEnumerable<string> links = null)
        {
            return new Dictionary<string, object>{ 
                { "collection" , items },
                { "_links",  links ?? new List<string>() }, //one day, turn this into HATEOS
                //_claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        // ************
        // * OVERRIDE *
        // ************

        protected virtual Task<IQueryable<T>> GetAllBase()
        {
            return Task.FromResult(services.context.Set<T>().Include(x => x.Entity).ThenInclude(x => x.AccessList).AsQueryable());
        }

        //protected virtual void SetNewEntity(T item)
        //{
        //    item.Entity = new Entity()
        //    {
        //        createDate = DateTime.Now,
        //        id = 0,
        //        status = 0
        //    };
        //}

        //protected T ConvertFromView(V view)
        //{
        //    //First, fill the easy stuff by creating a new entity thing from the view's basic fields
        //    var result = services.mapper.Map<T>(view);

        //    //Fill up a new entity
        //    SetNewEntity(result);

        //    //Now convert the special stuff.
        //    try
        //    {
        //        services.access.FillEntityAccess(result, view);
        //    }
        //    catch(Exception ex)
        //    {
        //        ThrowAction(BadRequest(ex.Message));
        //    }

        //    return result;
        //}

        protected virtual Task GetSingle_PreResultCheckAsync(T item) { return Task.CompletedTask; }

        //protected virtual Task Post_PreConversionCheckAsync(V item) { return Task.CompletedTask; }
        protected virtual Task<T> Post_ConvertItemAsync(V item) 
        { 
            try
            {
                return Task.FromResult(services.entity.ConvertFromView<T,V>(item)); //ConvertFromView(item);
            }
            catch(InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
                throw ex; //compiler plz (this is actually thrown above)
                //return Task.FromResult((T)null); //compiler plz
            }
        }
        //protected virtual Task Post_PreInsertCheckAsync(T item) { return Task.CompletedTask; }

        //protected virtual Task Put_PreConversionCheckAsync(V item, T existing) 
        //{ 
        //    item.createDate = existing.Entity.createDate;
        //    item.id = existing.entityId;
        //    return Task.CompletedTask;
        //}
        //protected virtual T Put_ConvertItem(V item, T existing) { return services.mapper.Map<V, T>(item, existing); }
        //protected virtual Task Put_PreInsertCheckAsync(T existing) { return Task.CompletedTask; }

        //protected virtual Task Delete_PreDeleteCheckAsync(T existing) { return Task.CompletedTask; }

        public async virtual Task<IQueryable<T>> GetQueryAsync(CollectionQuery query)
        {
            //Do stuff in between these (maybe?) in the future or... something.
            IQueryable<T> baseResults = await GetAllBase();
            IQueryable<T> queryResults = null;

            try
            {
                queryResults = services.query.ApplyQuery(baseResults, query);
            }
            catch (InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
            }

            return queryResults;
        }

        [HttpGet]
        [AllowAnonymous]
        public async virtual Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]CollectionQuery query)
        {
            try
            {
                var result = await GetQueryAsync(query);
                var views = (await result.ToListAsync()).Select(x => services.entity.ConvertFromEntity<T, V>(x));
                return GetGenericCollectionResult(views);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async virtual Task<ActionResult<V>> GetSingle(long id)
        {
            try
            {
                var results = await GetQueryAsync(new CollectionQuery() {ids = id.ToString()});
                var item = await results.FirstAsync();
                await GetSingle_PreResultCheckAsync(item);
                await LogAct(EntityAction.View, item.Entity.id);
                return services.mapper.Map<V>(item);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
            catch(Exception)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async virtual Task<ActionResult<V>> Post([FromBody]V item)
        {
            try
            {
                //Convert the user-provided object into a real one. Controllers can perform 
                //checks and whatever on objects during the conversion.
                var newThing = await Post_ConvertItemAsync(item);

                //Actually add the object??
                await services.context.Set<T>().AddAsync(newThing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Create, newThing.entityId);

                return services.mapper.Map<V>(newThing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        //Note: I don't think you need "Patch" because the way the "put" conversion works just... works.
        /*[HttpPut("{id}")]
        public async virtual Task<ActionResult<V>> Put([FromRoute]long id, [FromBody]V item)
        {
            try
            {
                var existing = await GetExisting(id);

                //Next, perform some checks. If anything happens, we need to return the result.
                await Put_PreConversionCheck(item, existing);

                //Now actually "convert" the item by placing it "into" the existing (assume existing gets modified in-place?)
                Put_ConvertItem(item, existing);

                //After conversion, perform one last check before insertion
                await Put_PreInsertCheck(existing);

                //Actually update the object now? I hope???
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Update, existing.id);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }*/

        /*[HttpDelete("{id}")]
        public async virtual Task<ActionResult<V>> Delete([FromRoute]long id)
        {
            try
            {
                var existing = await GetExisting(id);

                await Delete_PreDeleteCheck(existing);

                existing.status |= (int)ModelStatus.Deleted;
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();
                await LogAct(LogAction.Delete, existing.id);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }*/
    }

    //public abstract class AccessController<T,V> : GenericController<T, V> where T : GenericAccessModel where V : GenericAccessView
    //{
    //    protected AccessService accessService;

    //    public AccessController(GenericControllerServices services, AccessService accessService) : base(services) 
    //    { 
    //        this.accessService = accessService;
    //    }

    //    //protected abstract IQueryable<T> IncludeAccess(IQueryable<T> query) ;

    //    protected void CheckAccessFormat(GenericAccessView accessView)
    //    {
    //        if(!accessService.CheckAccessFormat(accessView))
    //            ThrowAction(BadRequest("Malformed access string (CRUD)"));
    //    }

    //    //Note: each accessor will need to figure out its own create check (since it'll be the parent)
    //    protected override async Task Post_PreConversionCheck(V view)
    //    {
    //        await base.Post_PreConversionCheck(view);
    //        CheckAccessFormat(view);
    //    }

    //    //Check Update privilege while checking the view's access format
    //    protected override async Task Put_PreConversionCheck(V view, T existing)
    //    {
    //        await base.Put_PreConversionCheck(view, existing);
    //        CheckAccessFormat(view);

    //        if(!accessService.CanUpdate(existing, await GetCurrentUserAsync()))
    //            ThrowAction(Unauthorized("You do not have permission to update this record"));

    //        if(view.accessList.Count > 0)
    //        {
    //            var userIds = view.accessList.Select(x => x.Key);
    //            var users = await services.context.Users.Where(x => userIds.Contains(x.id)).ToListAsync();

    //            if(users.Count != view.accessList.Count)
    //                ThrowAction(BadRequest("Bad access list: nonexistent / duplicate user"));
    //        }
    //    }

    //    //Check Read privilege before sending the result
    //    protected override async Task GetSingle_PreResultCheck(T model)
    //    {
    //        if(!accessService.CanRead(model, await GetCurrentUserAsync()))
    //            ThrowAction(Unauthorized("You do not have permission to read this record"));
    //    }

    //    //Check Delete privilege before deleting
    //    protected override async Task Delete_PreDeleteCheck(T model)
    //    {
    //        if(!accessService.CanDelete(model, await GetCurrentUserAsync()))
    //            ThrowAction(Unauthorized("You do not have permission to delete this record"));
    //    }

    //    //Filter results to remove ones we can't read
    //    protected override async Task<IQueryable<T>> Get_GetBase()
    //    {
    //        var user = await GetCurrentUserAsync();
    //        var result = await base.Get_GetBase();
    //        //Apply magic include BEFORE checking if you can read (I think order matters). HOWEVER: this tolist is BAD!!!
    //        result = result.Include("AccessList"); //(await IncludeAccess(result).ToListAsync());
    //        return result.Where(x => accessService.CanRead(x, user));
    //    }
    //}
}