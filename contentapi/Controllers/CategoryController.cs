using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public class CategoryController : BaseViewServiceController<CategoryView, CategorySearch>
    {
        public CategoryController(Keys keys, ILogger<BaseSimpleController> logger, IViewService<CategoryView, CategorySearch> service) 
            : base(keys, logger, service) { }
    }
}