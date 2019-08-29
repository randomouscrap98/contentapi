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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public abstract class GenericController<T,V> : ControllerBase where T : GenericModel where V : GenericModel
    {
        private ContentDbContext context;
        private IMapper mapper;

        public GenericController(ContentDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        //You MUST say how to get your objects! They're probably from the context!
        public abstract DbSet<T> GetObjects();
        
        //public virtual T AlterPostConvertedItem(T converted)
        //{
        //    return converted;
        //}

        //public virtual V AlterPostItem(V view)
        //{
        //    return view;
        //}

        public virtual Task<ActionResult<V>> Post_PreConversionCheck(V item)
        {
            return null;
        }

        public virtual Task<ActionResult<V>> Post_PreInsertCheck(T item)
        {
            return null;
        }

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
        public async Task<ActionResult<V>> Post([FromBody]V item)
        {
            var result = await Post_PreConversionCheck(item);

            if(result != null)
                return result;

            var newThing = mapper.Map<T>(item);

            result = await Post_PreInsertCheck(newThing);

            if(result != null)
                return result;

            GetObjects().Add(newThing);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSingle), new { id = newThing.id }, mapper.Map<V>(newThing));
        }
    }
}