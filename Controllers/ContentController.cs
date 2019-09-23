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
    public class ContentController : GenericController<Content, ContentView>
    {
        public ContentController(ContentDbContext context, IMapper mapper, PermissionService permissionService):base(context, mapper, permissionService) { }

        private bool CheckAccessFormat(string access)
        {
            //Why do this manually? idk...
            Dictionary<char, int> counts = new Dictionary<char, int>();

            foreach(var character in access)
            {
                if(character != 'C' && character != 'R' && character != 'U' && character != 'D')
                    return false;
                if(!counts.ContainsKey(character))
                    counts.Add(character, 0);
                if(++counts[character] > 1)
                    return false;
            }

            return true;
        }

        private void CheckAccessFormat(ContentView content)
        {
            if(!(CheckAccessFormat(content.baseAccess) && content.accessList.All(x => CheckAccessFormat(x.Value))))
                ThrowAction(BadRequest("Malformed access string (CRUD)"));
        }

        protected override Task Post_PreConversionCheck(ContentView content)
        {
            CheckAccessFormat(content);
            return Task.CompletedTask;
        }

        protected override Task Put_PreConversionCheck(ContentView content, Content existing)
        {
            CheckAccessFormat(content);
            return Task.CompletedTask;
        }

        protected override async Task Post_PreInsertCheck(Content content)
        {
            await base.Post_PreInsertCheck(content);

            if(content.AccessList.Count > 0)
            {
                var userIds = content.AccessList.Select(x => x.userId);
                var users = await context.Users.Where(x => userIds.Contains(x.id)).ToListAsync();

                if(users.Count != content.AccessList.Count)
                    ThrowAction(BadRequest("Bad access list: nonexistent / duplicate user"));
            }
        }
    }
}
