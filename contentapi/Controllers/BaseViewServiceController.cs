using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class BaseViewServiceController<T,V,S> : BaseSimpleController 
        where T : IViewService<V,S> where V : BaseView where S : EntitySearchBase
    {
        protected T service;

        public BaseViewServiceController(Keys keys, ILogger<BaseSimpleController> logger, T service) 
            : base(keys, logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<IList<V>>> GetAsync([FromQuery]S search)
        {
            logger.LogInformation($"GetAsync called, {typeof(V)}");
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<V>> PostAsync([FromBody]V view)
        {
            logger.LogInformation($"PostAsync called, {typeof(V)}");
            view.id = 0;
            return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<V>> PutAsync([FromRoute] long id, [FromBody]V view)
        {
            logger.LogInformation($"PutAsync called, {typeof(V)}({view.id})");
            view.id = id;
            return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        [HttpDelete("{id}")]
        [Authorize]
        public Task<ActionResult<V>> DeleteAsync([FromRoute]long id)
        {
            logger.LogInformation($"DeleteAsync called, {typeof(V)}({id})");
            return ThrowToAction(() => service.DeleteAsync(id, GetRequesterNoFail()));
        }
    }
}