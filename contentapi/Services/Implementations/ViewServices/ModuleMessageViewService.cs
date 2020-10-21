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

        public ModuleMessageViewService(ViewServicePack services, ILogger<ModuleMessageViewService> logger, 
            ModuleMessageViewSource moduleMessageSource)
            : base(services, logger) 
        { 
            this.moduleMessageSource = moduleMessageSource;
        }

        public override async Task<List<ModuleMessageView>> PreparedSearchAsync(ModuleMessageViewSearch search, Requester requester)
        {
            var result = await moduleMessageSource.SimpleSearchAsync(search, (q) =>
                q.Where(x => x.relation.entityId2 == 0 || x.relation.entityId2 == -requester.userId) //Can only get your own module messages
            );

            return result;
        }

        //A special endpoint for MODULES (not users) to add module messages
        public async Task<ModuleMessageView> AddMessageAsync(ModuleMessageView basic) //long senderuid, long receiveruid, string message, string module)
        {
            var relation = moduleMessageSource.FromView(basic);
            await provider.WriteAsync(relation);
            return moduleMessageSource.ToView(relation);
        }
    }
}