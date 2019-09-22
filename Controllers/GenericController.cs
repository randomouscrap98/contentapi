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

namespace contentapi.Controllers
{
    public class ActionCarryingException<T> : Exception
    {
        public ActionResult<T> Result;

        public ActionCarryingException() : base() { }
        public ActionCarryingException(string message) : base(message) {}
        public ActionCarryingException(string message, Exception inner) : base(message, inner) {}
    }

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GenericControllerRaw<T,V,P> : ControllerBase where T : GenericModel where V : class
    {
        protected ContentDbContext context;
        protected IMapper mapper;

        public GenericControllerRaw(ContentDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        protected void ThrowAction(ActionResult<V> result, string message = null)
        {
            if(message != null)
                throw new ActionCarryingException<V>(message) {Result = result};
            else
                throw new ActionCarryingException<V>() {Result = result};
        }

        protected long GetCurrentUid()
        {
            if(User == null)
                throw new InvalidOperationException("User is not set! Maybe there was no auth?");

            var value = User.FindFirstValue("uid");
            
            if(value == null)
                throw new InvalidOperationException("No uid field in User! Maybe there was no auth?");

            long result = 0;

            if(long.TryParse(User.FindFirstValue("uid"), out result))
                return result;
            else
                throw new InvalidOperationException("UID in incorrect format! How did this happen???");
        }

        protected virtual Task Post_PreConversionCheck(P item) { return Task.CompletedTask; }
        protected virtual T Post_ConvertItem(P item) { return mapper.Map<T>(item); }
        protected virtual Task Post_PreInsertCheck(T item) 
        { 
            //Make sure some fields are like... yeah
            item.createDate = DateTime.Now;
            item.id = 0;
            return Task.CompletedTask;
        }

        protected virtual Task Put_PreConversionCheck(P item, T existing) { return Task.CompletedTask; }
        protected virtual T Put_ConvertItem(P item, T existing) { return mapper.Map<P, T>(item, existing); }
        protected virtual Task Put_PreInsertCheck(T existing) { return Task.CompletedTask; }

        public Object GetGenericCollectionResult<W>(IEnumerable<W> items, IEnumerable<string> links = null) where W : GenericModel
        {
            return new { 
                collection = items.Select(x => mapper.Map<V>(x)),
                _links = links ?? new List<string>(), //one day, turn this into HATEOS
                _claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        [HttpGet]
        [AllowAnonymous]
        public async virtual Task<ActionResult<Object>> Get()
        {
            //Find a way to "fix" these results so you can do fancy sorting/etc.
            //Will we need this on every endpoint? Won't that be disgusting? How do we
            //make that "restful"? Look up pagination in REST
            return GetGenericCollectionResult<T>(await context.GetAll<T>().ToListAsync());
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async virtual Task<ActionResult<V>> GetSingle(long id)
        {
            try
            {
                var item = await context.GetSingleAsync<T>(id); //FindAsync(id);
                return mapper.Map<V>(item);
            }
            catch //(Exception ex)
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

                return CreatedAtAction(nameof(GetSingle), new { id = newThing.id }, mapper.Map<V>(newThing));
            }
            catch(ActionCarryingException<V> ex)
            {
                return ex.Result;
            }
        }

        [HttpPut("{id}")]
        public async virtual Task<ActionResult<V>> Put([FromRoute]long id, [FromBody]P item)
        {
            try
            {
                //var set = context.Set<T>();

                //First, see if our "existing" object (by id) even exists
                var existing = await context.GetSingleAsync<T>(id); //set.FirstAsync(x => x.id == id); //.FindAsync(id);

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

                return CreatedAtAction(nameof(GetSingle), new { id = existing.id }, mapper.Map<V>(existing));
            }
            catch(ActionCarryingException<V> ex)
            {
                return ex.Result;
            }
        }
    }

    public class GenericController<T,V> : GenericControllerRaw<T,V,V> where T : GenericModel where V : GenericView 
    {
        public GenericController(ContentDbContext context, IMapper mapper) : base(context, mapper){}
        protected override Task Put_PreConversionCheck(V item, T existing) 
        { 
            item.createDate = existing.createDate;
            item.id = existing.id;
            return Task.CompletedTask;
        }
    }
}