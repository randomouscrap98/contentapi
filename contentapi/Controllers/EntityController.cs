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
using Microsoft.Extensions.Logging;

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
    public class EntityControllerServices
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
        public ILoggerFactory logFactory;

        public EntityControllerServices(ContentDbContext context, IMapper mapper, 
            PermissionService permissionService, QueryService queryService,
            ISessionService sessionService, IEmailService emailService, 
            ILanguageService languageService, IHashService hashService,
            AccessService accessService, IEntityService entityService,
            ILoggerFactory logFactory)
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
            this.logFactory = logFactory;
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public abstract class EntityController<T,V> : ControllerBase where T : EntityChild where V : EntityView
    {
        protected EntityControllerServices services;
        protected ILogger logger;

        protected bool DoActionLog = true;

        public EntityController(EntityControllerServices services)
        {
            this.services = services;
            services.session.Context = this; //EEEWWWW
            this.logger = services.logFactory.CreateLogger(GetType());
        }

        // *************
        // * UTILITIES *
        // *************
        protected void ThrowAction(ActionResult result, string message = null)
        {
            logger.LogTrace($"Throwing action {result} : {message}");

            if(message != null)
                throw new ActionCarryingException(message) {Result = result};
            else
                throw new ActionCarryingException() {Result = result};
        }

        protected async Task LogAct(EntityAction action, long entityId, long? createUserOverride = null)
        {
            logger.LogTrace($"Logging action {action}({entityId})");

            var log = new EntityLog()
            {
                action = action,
                createDate = DateTime.Now,
                entityId = entityId,
                userId = createUserOverride ?? services.session.GetCurrentUid()
            };

            if(log.userId < 0)
                throw new InvalidOperationException("Cannot log actions for anonymous users!");

            await services.context.EntityLogs.AddAsync(log);
            await services.context.SaveChangesAsync();
        }

        public async Task<UserEntity> GetCurrentUserAsync()
        {
            return await services.context.UserEntities.FindAsync(services.session.GetCurrentUid());
        }

        public async Task<bool> CanUserAsync(Permission permission)
        {
            var user = await GetCurrentUserAsync();

            if(user == null)
                return false;

            return services.permission.CanDo(user.role, permission);
        }

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

        protected virtual async Task<IQueryable<T>> GetAllBase()
        {
            logger.LogTrace("GetAllBase called");
            long uid = services.session.GetCurrentUid();
            return services.access.WhereReadable(
                services.context.Set<T>()
                .Include(x => x.Entity)
                .ThenInclude(x => x.AccessList)
                .AsQueryable(), await GetCurrentUserAsync());
        }

        protected virtual async Task<T> GetSingleBase(long id)
        {
            logger.LogTrace($"GetSingleBase called for {id}");
            var results = await GetQueryAsync(new CollectionQuery() { ids = id.ToString() });
            return await results.FirstAsync();
        }

        protected virtual Task GetSingle_PreResultCheckAsync(T item) { return Task.CompletedTask; }

        protected virtual Task<T> Post_ConvertItemAsync(V item) 
        { 
            logger.LogTrace($"Post_ConvertItemAsync called with {item}");

            try
            {
                var entity = services.entity.ConvertFromView<T,V>(item);
                entity.Entity.userId = services.session.GetCurrentUid();
                return Task.FromResult(entity);
            }
            catch(InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
                throw ex; //compiler plz (this is actually thrown above)
            }
        }

        protected virtual Task Put_ConvertItemAsync(V item, T existing) 
        { 
            logger.LogTrace($"Put_ConvertItemAsync called with {item}");

            try
            {
                services.entity.FillExistingFromView(item, existing);
                return Task.CompletedTask;
            }
            catch(InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
                throw ex; //compiler plz (this is actually thrown above)
            }
        }

        protected virtual Task Delete_PrecheckAsync(T item)
        {
            return Task.CompletedTask;
        }

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
            logger.LogDebug($"Get called for {query}");

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
            logger.LogDebug($"GetSingle called for {id}");

            try
            {
                var item = await GetSingleBase(id); 
                await GetSingle_PreResultCheckAsync(item);
                await LogAct(EntityAction.View, item.Entity.id);
                return services.entity.ConvertFromEntity<T, V>(item);
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
            logger.LogDebug($"Post called for {item}");

            try
            {
                //Convert the user-provided object into a real one. Controllers can perform 
                //checks and whatever on objects during the conversion.
                var newThing = await Post_ConvertItemAsync(item);

                //Actually add the object??
                await services.context.Set<T>().AddAsync(newThing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Create, newThing.entityId);

                return services.entity.ConvertFromEntity<T,V>(newThing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        //Note: I don't think you need "Patch" because the way the "put" conversion works just... works.
        [HttpPut("{id}")]
        public async virtual Task<ActionResult<V>> Put([FromRoute]long id, [FromBody]V item)
        {
            logger.LogDebug($"Put called for {id}, {item}");

            try
            {
                var existing = await GetSingleBase(id);

                if(!services.access.CanUpdate(existing.Entity, await GetCurrentUserAsync()))
                    return Unauthorized("You cannot update this item!");

                //Now actually "convert" the item by placing it "into" the existing (assume existing gets modified in-place?)
                await Put_ConvertItemAsync(item, existing);

                //Actually update the object now? I hope???
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Update, existing.entityId);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        [HttpDelete("{id}")]
        public async virtual Task<ActionResult<V>> Delete([FromRoute]long id)
        {
            logger.LogDebug($"Delete called for {id}");

            try
            {
                var existing = await GetSingleBase(id);

                if(!services.access.CanDelete(existing.Entity, await GetCurrentUserAsync()))
                    return Unauthorized("You cannot delete this item!");

                await Delete_PrecheckAsync(existing);

                existing.Entity.status |= EntityStatus.Deleted;
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();
                await LogAct(EntityAction.Delete, existing.entityId);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }
    }
}