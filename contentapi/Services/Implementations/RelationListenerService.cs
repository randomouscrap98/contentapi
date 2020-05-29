using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class RelationListenConfig
    {
        public long lastId {get;set;} = -1;

        /// <summary>
        /// What your "status" should be in each room (arbitrary string yes)
        /// </summary>
        /// <value></value>
        public Dictionary<long, string> statuses {get;set;} = new Dictionary<long, string>();
    }

    public class RelationListener
    {
        public long userId {get;set;}

        //this is a mapping of contentId to personal status in content
        public Dictionary<long, string> listenStatuses {get;set;} = new Dictionary<long, string>();

        public override bool Equals(object obj)
        {
            if(obj != null && obj is RelationListener)
            {
                var listener = (RelationListener)obj;

                //WARN: A listener is "equal" to another (in terms of the DECAYER)
                //when it simply has the same user id! this is NOT PROPER EQUALS!
                return listener.userId == userId;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return userId.GetHashCode();
        }

        public override string ToString()
        {
            return $"u{userId}-c{string.Join(",", listenStatuses.Keys)}";
        }
    }

    //When registering this, make it a singleton
    public class RelationListenerService
    {
        protected static IDecayer<RelationListener> listenDecayer = null;
        protected static readonly object listenDecayLock = new object();
        protected IEntityProvider provider;
        protected SystemConfig config;
        protected ILogger logger;

        protected TimeSpan listenerPollingInterval = TimeSpan.FromSeconds(2);

        //Don't know what to do with this yet...
        protected IEnumerable<string> relationTypes = new[] { Keys.CommentHack, Keys.CommentDeleteHack, Keys.CommentHistoryHack };

        public RelationListenerService(ILogger<RelationListenerService> logger, IDecayer<RelationListener> decayer,
            IEntityProvider provider, SystemConfig config)
        {
            this.logger = logger;
            this.provider = provider;
            this.config = config;

            lock(listenDecayLock)
            {
                if (listenDecayer == null)
                    listenDecayer = decayer;
            }
        }

        public async Task<Dictionary<long, Dictionary<long, string>>> GetListenersAsync(Dictionary<long, Dictionary<long, string>> lastListeners, Requester requester, CancellationToken token)
        {
            DateTime start = DateTime.Now;

            //Creates a dictionary with pre-initialized keys. The keys won't change, we can keep redoing them.
            var result = lastListeners.ToDictionary(x => x.Key, y => new Dictionary<long, string>());

            while (DateTime.Now - start < config.ListenTimeout)
            {
                //Update listeners every IDK, some small time (every time we check).
                listenDecayer.UpdateList(GetListeners());

                //This list won't change as we're polling, so it's safe to keep writing over the old stuff.
                foreach(var parentKey in result.Keys.ToList())
                    result[parentKey] = listenDecayer.DecayList(config.ListenGracePeriod).Where(x => x.listenStatuses.ContainsKey(parentKey)).ToDictionary(x => x.userId, y => y.listenStatuses[parentKey]);

                if (result.Any(x => !x.Value.RealEqual(lastListeners[x.Key])))
                    return result;

                await Task.Delay(listenerPollingInterval, token);
                token.ThrowIfCancellationRequested();
            }

            throw new TimeoutException("Ran out of time waiting for listeners");
        }

        protected List<RelationListener> GetListeners(long parentId = -1)
        {
            var realListeners = provider.Listeners.Where(x => x.ListenerId is RelationListener).Select(x => (RelationListener)x.ListenerId);
            
            if(parentId > 0)
                realListeners = realListeners.Where(x => x.listenStatuses.ContainsKey(parentId));
                
            return realListeners.ToList();
        }

        public async Task<List<EntityRelation>> ListenAsync(RelationListenConfig listenConfig, Requester requester, CancellationToken token)
        {
            var listenId = new RelationListener() 
            { 
                userId = requester.userId,
                listenStatuses = listenConfig.statuses
            } ;

            if(listenConfig.lastId < 0)
                listenConfig.lastId = await provider.GetQueryable<EntityRelation>().MaxAsync(x => x.id);

            var results = await provider.ListenAsync<EntityRelation>(listenId, 
                (q) => q.Where(x => (EF.Functions.Like(x.type, $"{Keys.ActivityKey}%") || relationTypes.Contains(x.type)) && x.id > listenConfig.lastId), 
                config.ListenTimeout, token);

            return results; 
        }
    }
}