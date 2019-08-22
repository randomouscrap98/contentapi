using System.Collections.Generic;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private ContentDbContext context;
        private IMapper mapper;

        public UsersController(ContentDbContext context, IMapper mapper)
        {
            this.context = context;
            this.mapper = mapper;
        }

        [HttpGet]
        public ActionResult<IEnumerable<UserView>> Get()
        {
            return context.Users.Select(x => mapper.Map<UserView>(x)).ToList();
        }
    }
}