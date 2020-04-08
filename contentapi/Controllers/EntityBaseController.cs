using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public abstract class EntityBaseController<V> : ProviderBaseController
    {
        public EntityBaseController(ILogger<EntityBaseController<V>> logger, IEntityProvider provider, IMapper mapper)
            :base(logger, provider, mapper)
        { 

        }

        protected abstract V GetViewFromExpanded(EntityWrapper user);
    }
}