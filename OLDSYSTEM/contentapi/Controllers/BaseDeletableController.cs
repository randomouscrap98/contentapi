using System.Threading.Tasks;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public abstract class BaseDeletableController<V>: BaseSimpleController
    {
        protected BaseDeletableController(BaseSimpleControllerServices services) : base(services)  { }
            //ILogger<BaseDeletableController<V>> logger, UserValidationService userValidation) 
            //: base(logger, userValidation) { }

        protected abstract Task<ActionResult<V>> DeleteAsync(long id);


        [HttpDelete("{id}")] [Authorize]
        public Task<ActionResult<V>> DeleteDeleteAsync([FromRoute]long id) { return DeleteAsync(id); }

        [HttpPost("{id}/delete")] [Authorize]
        public Task<ActionResult<V>> DeletePostAsync([FromRoute]long id) { return DeleteAsync(id); }
    }
}