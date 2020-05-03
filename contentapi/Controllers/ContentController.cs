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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public class ContentController : BaseViewServiceController<ContentView, ContentSearch>
    {
        public ContentController(Keys keys, ILogger<BaseSimpleController> logger, IViewService<ContentView, ContentSearch> service) 
            : base(keys, logger, service) { }
    }
}