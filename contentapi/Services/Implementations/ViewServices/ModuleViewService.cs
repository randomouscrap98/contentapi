using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using AutoMapper;
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;
using contentapi.Services.Constants;
using System;
using System.Threading;
using Randomous.EntitySystem;
using System.Linq;

namespace contentapi.Services.Implementations
{
    public class ModuleMessage
    {
        private static long GlobalId = 0;

        public DateTime date {get;set;} = DateTime.Now;
        public long id = Interlocked.Increment(ref GlobalId);
        public string message {get;set;}
        public string module {get;set;}
        public long receiverUid = -1;
    }

    public class ModuleViewService : BaseEntityViewService<ModuleView, ModuleSearch>
    {
        protected ISignaler<ModuleMessage> signaler;

        //This is some in-memory mega list, just imagine it was persistent instead
        private static List<ModuleMessage> privateMessages = new List<ModuleMessage>();
        private static readonly object messageLock = new object();

        public ModuleViewService(ILogger<ModuleViewService> logger, ViewServicePack services, ModuleViewSource converter, 
            ISignaler<ModuleMessage> signaler) :base(services, logger, converter) 
        { 
            this.signaler = signaler;
        }

        public override string EntityType => Keys.ModuleType;

        public override async Task<ModuleView> CleanViewGeneralAsync(ModuleView view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Only supers can create modules!");
            return view;
        }

        public override Task<List<ModuleView>> PreparedSearchAsync(ModuleSearch search, Requester requester)
        {
            //NO permissions check! All modules are readable!
            return converter.SimpleSearchAsync(search);
        }

        public void AddMessage(ModuleMessage message)
        {
            lock(messageLock)
            {
                privateMessages.Add(message);
                signaler.SignalItems(new[] { message });
            }
        }
        
        public async Task<List<ModuleMessage>> ListenAsync(long lastId, Requester requester, TimeSpan maxWait, CancellationToken token)
        {
            Func<ModuleMessage, bool> filter = m => m.id > lastId && m.receiverUid == requester.userId;

            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                //We MUST start listening FIRST so we DON'T miss anything AT ALL (we could miss valuable signals that occur while reading initially)
                var listener = signaler.ListenAsync(requester, filter, maxWait, linkedCts.Token);

                DateTime start = DateTime.Now; //Putting this down here to minimize startup time before listen (not that this little variable really matters)
                var results = privateMessages.Where(filter).ToList();

                if (results.Count > 0)
                {
                    linkedCts.Cancel();

                    try
                    {
                        //Yes, we are so confident that we don't even worry about waiting properly
                        await listener;
                    }
                    catch(OperationCanceledException) {} //This is expected

                    return results;
                }
                else
                {
                    return (await listener).Cast<ModuleMessage>().ToList();
                }
            }
        }
    }
}