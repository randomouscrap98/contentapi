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
    public class CategoriesController : GenericController<Category, CategoryView>
    {
        public CategoriesController(ContentDbContext context, IMapper mapper, PermissionService permissionService):base(context, mapper, permissionService) { }

        protected override async Task Post_PreInsertCheck(Category category)
        {
            await base.Post_PreInsertCheck(category);

            if(!await CanUserAsync(Permission.CreateCategory))
                ThrowAction(Unauthorized("No permission to create categories"));

            if(category.parentId != null)
            {
                try
                {
                    var parentCategory = await context.GetSingleAsync<Category>((long)category.parentId); //context.GetAll()//Categories.FindAsync(category.parentId);
                }
                catch
                {
                    ThrowAction(BadRequest("Nonexistent parent category!"));
                }
            }
        }
    }
}