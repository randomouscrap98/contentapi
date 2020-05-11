using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Services.Views;
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

        public BaseViewServiceController(ILogger<BaseSimpleController> logger, T service) 
            : base(logger)
        {
            this.service = service;
        }

        protected virtual Task SetupAsync()
        {
            return Task.CompletedTask;
        }

        [HttpGet]
        public async Task<ActionResult<IList<V>>> GetAsync([FromQuery]S search)
        {
            logger.LogInformation($"GetAsync called, {typeof(V)}");
            await SetupAsync();
            return await ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<V>> PostAsync([FromBody]V view)
        {
            logger.LogInformation($"PostAsync called, {typeof(V)}");
            view.id = 0;
            await SetupAsync();
            return await ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<V>> PutAsync([FromRoute] long id, [FromBody]V view)
        {
            logger.LogInformation($"PutAsync called, {typeof(V)}({view.id})");
            view.id = id;
            await SetupAsync();
            return await ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult<V>> DeleteAsync([FromRoute]long id)
        {
            logger.LogInformation($"DeleteAsync called, {typeof(V)}({id})");
            await SetupAsync();
            return await ThrowToAction(() => service.DeleteAsync(id, GetRequesterNoFail()));
        }
    }
}