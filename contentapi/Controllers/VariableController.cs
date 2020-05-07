using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    //Every dang thing here needs you to be logged in (for now)
    [Authorize]
    public class VariableController : BaseSimpleController
    {
        protected ConcurrentDictionary<long, object> userlocks = new ConcurrentDictionary<long, object>();
        protected IEntityProvider provider;

        public VariableController(Keys keys, ILogger<BaseSimpleController> logger, IEntityProvider provider) : base(keys, logger)
        {
            this.provider = provider;
        }

        [HttpGet]
        public async Task<ActionResult<List<string>>> GetAsync()
        {
            var search = new EntityValueSearch()
            {
                KeyLike = keys.VariableKey + "%",
            };

            search.EntityIds.Add(-GetRequesterUid());

            var baseValues = provider.GetQueryable<EntityValue>();
            var searchValues = provider.ApplyEntityValueSearch(baseValues, search);

            return await provider.GetListAsync(searchValues.Select(x => x.key.Substring(keys.VariableKey.Length)));
        }

        protected async Task<EntityValue> GetVariable(string key)
        {
            var uid = GetRequesterUid();

            var query = 
                from v in provider.GetQueryable<EntityValue>()
                where EF.Functions.Like(v.key, keys.VariableKey + key) && v.entityId == -uid
                join e in provider.GetQueryable<Entity>() on -v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{keys.UserType}%")
                select v;
                //The JOIN is REQUIRED because we had some issues in the past of values getting
                //duplicated across history keys. Now this code is stuck here forever until the database
                //gets cleaned up.

            return (await provider.GetListAsync(query)).OnlySingle();
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<string>> GetSingleAsync([FromRoute]string key)
        {
            logger.LogDebug($"{GetRequesterUidNoFail()} varget {key}");
            var result = await GetVariable(key);
            if(result == null)
                return NotFound();
            return result.value;
        }

        [HttpPost("{key}")]
        public ActionResult<string> Post([FromRoute]string key, [FromBody]string data)
        {
            //Because we have a single combined interface that MIGHt need to create or update, need to lock
            var uid = GetRequesterUid();

            lock(userlocks.GetOrAdd(uid, l => new object()))
            {
                var existing = GetVariable(key).Result;

                if (existing == null)
                {
                    existing = new EntityValue() { key = keys.VariableKey + key, value = data };
                    existing.entityId = -uid;
                }
                else
                {
                    existing.value = data;
                }

                provider.WriteAsync(existing).Wait();

                return existing.value;
            }
        }

        [HttpDelete("{key}")]
        public ActionResult<string> Delete([FromRoute] string key)
        {
            var uid = GetRequesterUid();
            lock(userlocks.GetOrAdd(uid, l => new object()))
            {
                var result = GetVariable(key).Result;
                if (result == null)
                    return NotFound();
                provider.DeleteAsync(result).Wait();
                return result.value;
            }
        }
    }
}