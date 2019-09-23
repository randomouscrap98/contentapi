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

        protected override async Task Post_PreInsertCheck(Content content)
        {
            await base.Post_PreInsertCheck(content);

            //if(User.Claims)
            //if(category.parentId != null)
            //{
            //    var parentCategory = await context.Categories.FindAsync(category.parentId);

            //    if(parentCategory == null)
            //        ThrowAction(BadRequest("Nonexistent parent category!"));
            //}
        }
    }
}
