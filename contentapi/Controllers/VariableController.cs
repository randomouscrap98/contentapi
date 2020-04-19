using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    //Every dang thing here needs you to be logged in (for now)
    [Authorize]
    public class VariableController : BaseSimpleController
    {
        public VariableController(ControllerServices services, ILogger<BaseSimpleController> logger) : base(services, logger)
        {
        }

        [HttpGet]
        public async Task<ActionResult<List<string>>> GetAsync()
        {
            var search = new EntityValueSearch()
            {
                KeyLike = keys.VariableKey + "%",
            };

            search.EntityIds.Add(GetRequesterUid());

            var baseValues = services.provider.GetQueryable<EntityValue>();
            var searchValues = services.provider.ApplyEntityValueSearch(baseValues, search);

            return await services.provider.GetListAsync(searchValues.Select(x => x.key.Substring(keys.VariableKey.Length)));
        }

        protected async Task<EntityValue> GetVariable(string key)
        {
            return await services.provider.FindValueAsync(keys.VariableKey + key, null, GetRequesterUid());
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<string>> GetSingleAsync([FromRoute]string key)
        {
            var result = await GetVariable(key);
            if(result == null)
                return NotFound();
            return result.value;
        }

        [HttpPost("{key}")]
        public async Task<ActionResult<string>> PostAsync([FromRoute]string key, [FromBody]string data)
        {
            var existing = await GetVariable(key);

            if(existing == null)
            {
                existing = NewValue(keys.VariableKey + key, data);
                existing.entityId = GetRequesterUid();
            }
            else
            {
                existing.value = data;
            }
            
            await services.provider.WriteAsync(existing);
            
            return existing.value;
        }

        [HttpDelete("{key}")]
        public async Task<ActionResult<string>> DeleteAsync([FromRoute] string key)
        {
            var result = await GetVariable(key);
            if(result == null)
                return NotFound();
            await services.provider.DeleteAsync(result);
            return result.value;
        }
    }
}