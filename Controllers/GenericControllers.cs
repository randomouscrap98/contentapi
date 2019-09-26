using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using contentapi.Models;
using contentapi.Services;

namespace contentapi.Controllers
{
    //Query parameters (from url) for searching/querying collections
    public class CollectionQuery
    {
        public int offset {get;set;} = 0;
        public int count {get;set;} = 0;
        public string sort {get;set;} = "";
        public string order {get;set;} = "";
    }

    public class ActionCarryingException : Exception
    {
        public ActionResult Result;

        public ActionCarryingException() : base() { }
        public ActionCarryingException(string message) : base(message) {}
        public ActionCarryingException(string message, Exception inner) : base(message, inner) {}
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public abstract class GenericControllerRaw<T,V,P> : ControllerBase where T : GenericModel where V : class
    {
        //These may need to be configurable one day
        public const int DefaultResultCount = 1000;
        public const int MaxResultCount = 5000;
        public const string IdSort = "id";
        public const string CreateSort = "create";
        public const string AscendingOrder = "asc";
        public const string DescendingOrder = "desc";

        protected ContentDbContext context;
        protected IMapper mapper;
        protected PermissionService permissionService;

        protected bool DoActionLog = true;


        public GenericControllerRaw(ContentDbContext context, IMapper mapper, PermissionService permissionService)
        {
            this.context = context;
            this.mapper = mapper;
            this.permissionService = permissionService;
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

        protected async Task LogAct(LogAction action, Action<ActionLog> setField)
        {
            //Do NOT LOG if we're not set to
            if(!DoActionLog)
                return;

            var log = new ActionLog()
            {
                action = action,
                createDate = DateTime.Now,
                contentId = null,
                categoryId = null,
                userId = null
            };

            try
            {
                log.actionUserId = GetCurrentUid();
            }
            catch
            {
                //Eventually we can log here... when are we adding logging?
                return;
            }

            setField(log);

            await context.Logs.AddAsync(log);
            await context.SaveChangesAsync();
        }

        protected async Task LogAct(LogAction action, long id)
        {
            await LogAct(action, (l) => SetLogField(l, id));
        }

        protected string GetCurrentField(string field)
        {
            if(User == null)
                throw new InvalidOperationException("User is not set! Maybe there was no auth?");

            var value = User.FindFirstValue(field);
            
            if(value == null)
                throw new InvalidOperationException($"No {field} field in User! Maybe there was no auth?");

            return value;
        }

        protected long GetCurrentUid()
        {
            try
            {
                return long.Parse(GetCurrentField("uid"));
            }
            catch(Exception)
            {
                //TODO: LOGGING GOES HERE!
                return -1;
            }
        }

        protected async Task<User> GetCurrentUserAsync()
        {
            return await context.Users.FindAsync(GetCurrentUid());
        }

        protected async Task<bool> CanUserAsync(Permission permission)
        {
            var user = await GetCurrentUserAsync();

            if(user == null)
                return false;

            return permissionService.CanDo((Role)user.role, permission);
        }

        //How to RETURN items (the object we return... maybe make it a real class)
        public Object GetGenericCollectionResult<W>(IEnumerable<W> items, IEnumerable<string> links = null)
        {
            return new { 
                collection = items, //items.Select(x => mapper.Map<V>(x)),
                _links = links ?? new List<string>(), //one day, turn this into HATEOS
                _claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        //I have NO idea if this IQueryable set is SUPER slow.... guess we'll see.
        public IQueryable<W> ApplyQuery<W>(IQueryable<W> originSet, CollectionQuery query) where W : GenericModel
        {
            //Set some nice defaults for query parameters
            if(query.count <= 0)
                query.count = DefaultResultCount;

            if(query.count > MaxResultCount)
                ThrowAction(BadRequest($"Too many objects! Max: {MaxResultCount}"));

            if(string.IsNullOrWhiteSpace(query.sort))
                query.sort = CreateSort;

            var order = query.order.ToLower();
            IQueryable<W> orderedSet = originSet;
            System.Linq.Expressions.Expression<Func<W, object>> sorter = GetSorter<W>(query.sort);

            if(sorter != null)
            {
                if (string.IsNullOrWhiteSpace(order) || order == AscendingOrder)
                    orderedSet = orderedSet.OrderBy(sorter);
                else if (order == DescendingOrder)
                    orderedSet = orderedSet.OrderByDescending(sorter);
                else
                    ThrowAction(BadRequest($"Unknown order type ({AscendingOrder}/{DescendingOrder})"));
            }

            IQueryable<W> slicedSet = orderedSet;

            try
            {
                slicedSet = slicedSet.Skip(query.offset).Take(query.count);
            }
            catch
            {
                ThrowAction(BadRequest("Offset/count broke set; this is API laziness"));
            }

            return slicedSet;
        }

        // ************
        // * OVERRIDE *
        // ************

        //GOTTA OVERRIDE THIS 
        protected abstract void SetLogField(ActionLog log, long id);

        protected virtual Task<IQueryable<T>> Get_GetBase() { return Task.FromResult((IQueryable<T>)context.Set<T>()); }
        protected virtual Task GetSingle_PreResult(T item) { return Task.CompletedTask; }

        protected virtual Task Post_PreConversionCheck(P item) { return Task.CompletedTask; }
        protected virtual T Post_ConvertItem(P item) { return mapper.Map<T>(item); }
        protected virtual Task Post_PreInsertCheck(T item) 
        { 
            //Make sure some fields are like... yeah
            item.createDate = DateTime.Now;
            item.id = 0;
            item.status = 0;
            return Task.CompletedTask;
        }

        protected virtual Task Put_PreConversionCheck(P item, T existing) { return Task.CompletedTask; }
        protected virtual T Put_ConvertItem(P item, T existing) { return mapper.Map<P, T>(item, existing); }
        protected virtual Task Put_PreInsertCheck(T existing) { return Task.CompletedTask; }

        protected virtual System.Linq.Expressions.Expression<Func<W, object>> GetSorter<W>(string sort) where W : GenericModel 
        {
            if(sort == IdSort)
                return (x) => ((GenericModel)x).id;
            else if(sort == CreateSort)
                return (x) => ((GenericModel)x).createDate;

            return null;
        }

        [HttpGet]
        [AllowAnonymous]
        public async virtual Task<ActionResult<Object>> Get([FromQuery]CollectionQuery query)
        {
            try
            {
                //Do stuff in between these (maybe?) in the future or... something.
                var baseResults = await Get_GetBase();
                var queryResults = ApplyQuery(baseResults, query);
                var views = (await queryResults.ToListAsync()).Select(x => mapper.Map<V>(x));
                return GetGenericCollectionResult(views); //(await queryResults.ToListAsync()).Select(x => x.));
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
                var item = await context.GetSingleAsync<T>(id);
                await GetSingle_PreResult(item);
                await LogAct(LogAction.View, id);
                return mapper.Map<V>(item);
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
        public async virtual Task<ActionResult<V>> Post([FromBody]P item)
        {
            try
            {
                //Check the passed-in object. If anything happens, stop now
                await Post_PreConversionCheck(item);

                //Convert the user-provided object into a real one
                var newThing = Post_ConvertItem(item);

                //Perform one last check on the converted item
                await Post_PreInsertCheck(newThing);

                //Actually add the object??
                await context.Set<T>().AddAsync(newThing);
                await context.SaveChangesAsync();

                await LogAct(LogAction.Create, newThing.id);

                return CreatedAtAction(nameof(GetSingle), new { id = newThing.id }, mapper.Map<V>(newThing));
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        [HttpPut("{id}")]
        public async virtual Task<ActionResult<V>> Put([FromRoute]long id, [FromBody]P item)
        {
            try
            {
                //First, see if our "existing" object (by id) even exists
                var existing = await context.GetSingleAsync<T>(id); 

                if (existing == null)
                    return NotFound();

                //Next, perform some checks. If anything happens, we need to return the result.
                await Put_PreConversionCheck(item, existing);

                //Now actually "convert" the item by placing it "into" the existing (assume existing gets modified in-place?)
                Put_ConvertItem(item, existing);

                //After conversion, perform one last check before insertion
                await Put_PreInsertCheck(existing);

                //Actually update the object now? I hope???
                context.Set<T>().Update(existing);
                await context.SaveChangesAsync();

                await LogAct(LogAction.Update, existing.id);

                return CreatedAtAction(nameof(GetSingle), new { id = existing.id }, mapper.Map<V>(existing));
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }
    }

    public abstract class GenericController<T,V> : GenericControllerRaw<T,V,V> where T : GenericModel where V : GenericView 
    {
        public GenericController(ContentDbContext context, IMapper mapper, PermissionService permissionService) : base(context, mapper, permissionService){}

        protected override async Task Put_PreConversionCheck(V item, T existing) 
        { 
            await base.Put_PreConversionCheck(item, existing);
            item.createDate = existing.createDate;
            item.id = existing.id;
        }
    }

    public abstract class AccessController<T,V> : GenericController<T, V> where T : GenericAccessModel where V : GenericAccessView
    {
        protected AccessService accessService;

        public AccessController(ContentDbContext c, IMapper m, PermissionService p, AccessService accessService) : base(c, m, p) 
        { 
            this.accessService = accessService;
        }

        protected void CheckAccessFormat(GenericAccessView accessView)
        {
            if(!accessService.CheckAccessFormat(accessView))
                ThrowAction(BadRequest("Malformed access string (CRUD)"));
        }

        protected override async Task Post_PreConversionCheck(V view)
        {
            await base.Post_PreConversionCheck(view);
            CheckAccessFormat(view);
        }

        protected override async Task Put_PreConversionCheck(V view, T existing)
        {
            await base.Put_PreConversionCheck(view, existing);
            CheckAccessFormat(view);

            if(!accessService.CanUpdate(existing, await GetCurrentUserAsync()))
                ThrowAction(Unauthorized("You do not have permission to update this record"));
        }

        protected override async Task Put_PreInsertCheck(T existing) 
        { 
            await base.Put_PreInsertCheck(existing);
        }

        protected override async Task Post_PreInsertCheck(T model)
        {
            await base.Post_PreInsertCheck(model);

            if(model.GenericAccessList.Count > 0)
            {
                var userIds = model.GenericAccessList.Select(x => x.userId);
                var users = await context.Users.Where(x => userIds.Contains(x.id)).ToListAsync();

                if(users.Count != model.GenericAccessList.Count)
                    ThrowAction(BadRequest("Bad access list: nonexistent / duplicate user"));
            }
        }

        protected override async Task GetSingle_PreResult(T model)
        {
            if(!accessService.CanRead(model, await GetCurrentUserAsync()))
                ThrowAction(Unauthorized("You do not have permission to read this record"));
        }

        protected override async Task<IQueryable<T>> Get_GetBase()
        {
            var user = await GetCurrentUserAsync();
            return (await base.Get_GetBase()).Where(x => accessService.CanRead(x, user));
        }
    }
}