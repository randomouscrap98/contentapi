using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class BaseViewServiceController<T,V,S> : BaseDeletableController<V>
        where T : IViewService<V,S> where V : BaseView where S : BaseSearch
    {
        protected T service;

        //public BaseViewServiceController(ILogger<BaseDeletableController<V>> logger, T service, UserValidationService userValidation) 
        //    : base(logger, userValidation)
        public BaseViewServiceController(BaseSimpleControllerServices services, T service) : base(services)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<List<V>>> GetAsync([FromQuery]S search)
        {
            return ThrowToAction(() => service.SearchAsync(search, GetRequesterNoFail()));
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<V>> PostAsync([FromBody]V view)
        {
            view.id = 0;
            return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<V>> PutAsync([FromRoute] long id, [FromBody]V view)
        {
            view.id = id;
            return ThrowToAction(() => service.WriteAsync(view, GetRequesterNoFail()));
        }

        protected override Task<ActionResult<V>> DeleteAsync([FromRoute]long id)
        {
            return ThrowToAction(() => service.DeleteAsync(id, GetRequesterNoFail()));
        }
    }
}