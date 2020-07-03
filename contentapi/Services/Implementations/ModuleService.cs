using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

    public class ModuleService
    {
        protected ILogger logger;
        protected ISignaler<ModuleMessage> signaler;

        //This is some in-memory mega list, just imagine it was persistent instead
        private static List<ModuleMessage> privateMessages = new List<ModuleMessage>();
        private static readonly object messageLock = new object();

        public ModuleService(ILogger<ModuleService> logger, ISignaler<ModuleMessage> signaler)
        {
            this.logger = logger;
            this.signaler = signaler;
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