using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
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

        public VariableController(BaseSimpleControllerServices services, IEntityProvider provider) : base(services)
        {
            this.provider = provider;
        }

        [HttpGet]
        public async Task<ActionResult<List<string>>> GetAsync()
        {
            var search = new EntityValueSearch()
            {
                KeyLike = Keys.VariableKey + "%",
            };

            search.EntityIds.Add(-GetRequesterUid());

            var baseValues = await provider.GetQueryableAsync<EntityValue>();
            var searchValues = provider.ApplyEntityValueSearch(baseValues, search);

            return await provider.GetListAsync(searchValues.Select(x => x.key.Substring(Keys.VariableKey.Length)));
        }

        protected async Task<List<EntityValue>> GetVariables(IEnumerable<string> keys) //string key)
        {
            var uid = GetRequesterUid();

            var realKeys = keys.Select(x => Keys.VariableKey + x);

            var values = await provider.GetQueryableAsync<EntityValue>();
            var entities = await provider.GetQueryableAsync<Entity>();

            var query = 
                from v in values
                where realKeys.Contains(v.key) //&& v.entityId == -uid
                //where EF.Functions.Like(v.key, Keys.VariableKey + key) && v.entityId == -uid
                join e in entities on -v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{Keys.UserType}%") && e.id == uid
                select v;
                //The JOIN is REQUIRED because we had some issues in the past of values getting
                //duplicated across history Keys. Now this code is stuck here forever until the database
                //gets cleaned up.

            return await provider.GetListAsync(query);
        }

        protected async Task<EntityValue> GetVariable(string key)
        {
            return (await GetVariables(new[] {key})).OnlySingle();
        }

        [HttpGet("{key}")]
        public Task<ActionResult<string>> GetSingleAsync([FromRoute]string key)
        {
            return ThrowToAction(async () =>
            {
                logger.LogDebug($"{GetRequesterUidNoFail()} varget {key}");
                var result = await GetVariable( key );
                if(result == null)
                    throw new NotFoundException();
                return result.value;
            });
        }

        [HttpGet("multi")]
        public Task<ActionResult<Dictionary<string, string>>> GetMultiAsync([FromQuery]List<string> keys)
        {
            return ThrowToAction(async() =>
            {
                logger.LogDebug($"{GetRequesterUidNoFail()} vargetmulti {string.Join(",", keys)}");
                var result = await GetVariables(keys);
                if (result == null)
                    throw new NotFoundException();
                return result.ToDictionary(x => x.key.Substring(Keys.VariableKey.Length), x => x.value);
            });
        }

        [HttpPost("{key}")]
        public Task<ActionResult<string>> Post([FromRoute]string key, [FromBody]string data)
        {
            return ThrowToAction(() =>
            {
                //Because we have a single combined interface that MIGHt need to create or update, need to lock
                var uid = GetRequesterUid();

                lock (userlocks.GetOrAdd(uid, l => new object()))
                {
                    var existing = GetVariable(key).Result;

                    if (existing == null)
                    {
                        existing = new EntityValue() { key = Keys.VariableKey + key, value = data };
                        existing.entityId = -uid;
                    }
                    else
                    {
                        existing.value = data;
                    }

                    provider.WriteAsync(existing).Wait();

                    return Task.FromResult(existing.value);
                }
            });
        }

        [HttpDelete("{key}")]
        public Task<ActionResult<string>> Delete([FromRoute] string key)
        {
            return ThrowToAction(() =>
            {
                var uid = GetRequesterUid();
                lock (userlocks.GetOrAdd(uid, l => new object()))
                {
                    var result = GetVariable(key).Result;
                    if (result == null)
                        throw new NotFoundException();
                    provider.DeleteAsync(result).Wait();
                    return Task.FromResult(result.value);
                }
            });
        }
    }
}