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
    public abstract class GenericControllerRaw<T,V,P> : ControllerBase where T : GenericModel where V : class
    {
        protected ContentDbContext context;
        protected IMapper mapper;

        public GenericControllerRaw(ContentDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        //You MUST say how to get your objects! They're probably from the context!
        public abstract DbSet<T> GetObjects();
        
        protected void ThrowAction(ActionResult<V> result, string message = null)
        {
            if(message != null)
                throw new ActionCarryingException<V>(message) {Result = result};
            else
                throw new ActionCarryingException<V>() {Result = result};
        }

        protected virtual Task Post_PreConversionCheck(P item) { return Task.CompletedTask; }//.FromResult<ActionResult<V>>(null); }
        protected virtual T Post_ConvertItem(P item) { return mapper.Map<T>(item); }
        protected virtual Task Post_PreInsertCheck(T item) 
        { 
            //Make sure some fields are like... yeah
            item.createDate = DateTime.Now;
            item.id = 0;
            return Task.CompletedTask; //FromResult<ActionResult<V>>(null); 
        }

        protected virtual Task Put_PreConversionCheck(P item, T existing) { return Task.CompletedTask; }//.FromResult<ActionResult<V>>(null); }
        protected virtual T Put_ConvertItem(P item, T existing) { return mapper.Map<P, T>(item, existing); }
        protected virtual Task Put_PreInsertCheck(T existing) { return Task.CompletedTask; } //.FromResult<ActionResult<V>>(null); }

        [HttpGet]
        [AllowAnonymous]
        public async virtual Task<ActionResult<Object>> Get()
        {
            //Find a way to "fix" these results so you can do fancy sorting/etc.
            //Will we need this on every endpoint? Won't that be disgusting? How do we
            //make that "restful"? Look up pagination in REST
            return new { 
                collection = (await GetObjects().ToListAsync()).Select(x => mapper.Map<V>(x)),
                _links = new List<string>(), //one day, turn this into HATEOS
                _claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async virtual Task<ActionResult<V>> GetSingle(long id)
        {
            var item = await GetObjects().FindAsync(id);

            if(item == null)
                return NotFound();

            return mapper.Map<V>(item);
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
                await GetObjects().AddAsync(newThing);
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
                //First, see if our "existing" object (by id) even exists
                var existing = await GetObjects().FindAsync(id);

                if (existing == null)
                    return NotFound();

                //Next, perform some checks. If anything happens, we need to return the result.
                await Put_PreConversionCheck(item, existing);

                //Now actually "convert" the item by placing it "into" the existing (assume existing gets modified in-place?)
                Put_ConvertItem(item, existing);

                //After conversion, perform one last check before insertion
                await Put_PreInsertCheck(existing);

                //Actually update the object now? I hope???
                GetObjects().Update(existing);
                await context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetSingle), new { id = existing.id }, mapper.Map<V>(existing));
            }
            catch(ActionCarryingException<V> ex)
            {
                return ex.Result;
            }
        }
    }

    public abstract class GenericController<T,V> : GenericControllerRaw<T,V,V> where T : GenericModel where V : GenericModel
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