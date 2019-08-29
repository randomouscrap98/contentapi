using System.Collections.Generic;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private ContentDbContext context;
        private IMapper mapper;

        //protected UsersControllerConfig config;

        public CategoriesController(ContentDbContext context, IMapper mapper) //, UsersControllerConfig config)
        {
            this.context = context;
            this.mapper = mapper;
            //this.config = config;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<Object>> Get()
        {
            //Find a way to "fix" these results so you can do fancy sorting/etc.
            //Will we need this on every endpoint? Won't that be disgusting? How do we
            //make that "restful"? Look up pagination in REST
            return new { 
                categories = (await context.Categories.ToListAsync()).Select(x => mapper.Map<CategoryView>(x)),
                _links = new List<string>(), //one day, turn this into HATEOS
            };
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetSingle(long id)
        {
            var category = await context.Categories.FindAsync(id);
            var parent = category.Parent;
            var children = category.Children;

            if(category == null)
                return NotFound();

            return mapper.Map<CategoryView>(category);
        }

        [HttpPost]
        public async Task<ActionResult<CategoryView>> Post([FromBody]CategoryView category)
        {
            var newCategory = mapper.Map<Category>(category);
            newCategory.createDate = DateTime.Now;

            if(category.parentId != null)
            {
                var parentCategory = context.Categories.Find(category.parentId);
                if(parentCategory == null)
                    return BadRequest("Nonexistent parent category!");
            }

            context.Categories.Add(newCategory);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSingle), new { id = newCategory.id }, mapper.Map<CategoryView>(newCategory));
        }
    }
}