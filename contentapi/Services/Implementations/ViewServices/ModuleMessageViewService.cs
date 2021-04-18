using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class ModuleMessageViewService : BaseViewServices<ModuleMessageView, ModuleMessageViewSearch>, IViewReadService<ModuleMessageView, ModuleMessageViewSearch>
    {
        protected ModuleMessageViewSource moduleMessageSource;
        protected CacheService<long, ModuleMessageView> singlecache;

        public ModuleMessageViewService(ViewServicePack services, ILogger<ModuleMessageViewService> logger, 
            ModuleMessageViewSource moduleMessageSource, CacheService<long, ModuleMessageView> singlecache)
            : base(services, logger) 
        { 
            this.moduleMessageSource = moduleMessageSource;
            this.singlecache = singlecache;
        }

        public override async Task<List<ModuleMessageView>> PreparedSearchAsync(ModuleMessageViewSearch search, Requester requester)
        {
            List<ModuleMessageView> result = null;

            if(search.Ids.Count > 0 && OnlyIdSearch(search, requester))
            {
                result = singlecache.GetValues(search.Ids);

                //NOTE: there is a period of time where the cache could be invalid by the time you get this data. I'm willing
                //to accept slightly out of date information... but does that mean some people will NEVER get the updates? no,
                //it just means for THIS request, they may have something slightly amiss.
                if (result.Select(x => x.id).OrderBy(x => x).SequenceEqual(search.Ids.Distinct().OrderBy(x => x)))
                {
                    result.RemoveAll(x => !(x.receiveUserId == 0 || x.receiveUserId == requester.userId));
                    logger.LogDebug($"* Using in-memory module message ({string.Join(",", result.Select(x => x.id))}) for ids {string.Join(",", search.Ids)}");
                    return result;
                }
            }

            result = await moduleMessageSource.SimpleSearchAsync(search, (q) =>
                q.Where(x => x.relation.entityId2 == 0 || x.relation.entityId2 == -requester.userId) //Can only get your own module messages
            );

            return result;
        }

        //A special endpoint for MODULES (not users) to add module messages
        public async Task<ModuleMessageView> AddMessageAsync(ModuleMessageView basic) //long senderuid, long receiveruid, string message, string module)
        {
            var relation = moduleMessageSource.FromView(basic);
            await provider.WriteAsync(relation);
            var view = moduleMessageSource.ToView(relation);
            singlecache.StoreItem(view.id, view);
            return view;
        }
    }
}