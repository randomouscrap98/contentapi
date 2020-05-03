//using System.Threading.Tasks;
//using contentapi.Views;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using Randomous.EntitySystem;
//
//namespace contentapi.Controllers
//{
//    public abstract class BasePermissionActionController<V> : BasePermissionController<V> where V : BasePermissionView
//    {
//        public BasePermissionActionController(ControllerServices services, ILogger<BaseEntityController<V>> logger) : base(services, logger)
//        {
//        }
//
//        [HttpPost]
//        [Authorize]
//        public Task<ActionResult<V>> PostAsync([FromBody]V view)
//        {
//            logger.LogInformation($"PostAsync called, {typeof(V)}");
//            view.id = 0;
//            return ThrowToAction(() => WriteViewAsync(view));
//        }
//
//        [HttpPut("{id}")]
//        [Authorize]
//        public Task<ActionResult<V>> PutAsync([FromRoute] long id, [FromBody]V view)
//        {
//            logger.LogInformation($"PutAsync called, {typeof(V)}({view.id})");
//            view.id = id;
//            return ThrowToAction(() => WriteViewAsync(view));
//        }
//
//        [HttpDelete("{id}")]
//        [Authorize]
//        public Task<ActionResult<V>> DeleteAsync([FromRoute]long id)
//        {
//            logger.LogInformation($"DeleteAsync called, {typeof(V)}({id})");
//            return ThrowToAction(() => DeleteByIdAsync(id));
//        }
//    }
//}