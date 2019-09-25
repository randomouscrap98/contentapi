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
    }
}
