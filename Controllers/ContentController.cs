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
using contentapi.Services;

namespace contentapi.Controllers
{
    public class ContentController : AccessController<Content, ContentView>
    {
        public ContentController(ContentDbContext c, IMapper m, PermissionService p, AccessService a) : base(c, m, p, a) { }

        protected override void SetLogField(ActionLog log, long id) { log.contentId = id; }
        
        protected override async Task Post_PreConversionCheck(ContentView content)
        {
            await base.Post_PreConversionCheck(content);

            //Completely ignore whatever UID they gave us.
            content.userId = GetCurrentUid();
        }

        protected override async Task Post_PreInsertCheck(Content content)
        {
            await base.Post_PreInsertCheck(content);

            Category category = null;

            try
            {
                category = await context.GetSingleAsync<Category>((long)content.categoryId);
            }
            catch
            {
                ThrowAction(BadRequest("Must provide category for content!"));
            }

            var user = await GetCurrentUserAsync();

            if(!accessService.CanCreate(category, user))
                ThrowAction(Unauthorized("Can't create content in this category!"));
        }
    }
}
