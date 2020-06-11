using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        public List<long> clearNotifications {get;set;} = new List<long>();
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

                //This COULD get too computationally... bad! With LOTS of listeners all trying to update the list
                //every two seconds and if the list has hundreds of relations, this is a LOT of RealEqual calls, 
                //which is a lot of sorting and such!
                return listener.userId == userId && listenStatuses.RealEqual(listener.listenStatuses);
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

    public class RelationListenerServiceConfig
    {
        public TimeSpan ListenerPollingInterval = TimeSpan.FromSeconds(2);
    }

    //When registering this, make it a singleton
    public class RelationListenerService
    {
        protected IDecayer<RelationListener> listenDecayer = null;
        protected IEntityProvider provider;
        protected SystemConfig systemConfig;
        protected RelationListenerServiceConfig config;
        protected ILogger logger;


        public RelationListenerService(ILogger<RelationListenerService> logger, IDecayer<RelationListener> decayer,
            IEntityProvider provider, SystemConfig systemConfig, RelationListenerServiceConfig config)
        {
            this.logger = logger;
            this.provider = provider;
            this.systemConfig = systemConfig;
            this.config = config;

            this.listenDecayer = decayer;
        }

        public string FormatListeners(Dictionary<long,Dictionary<long,string>> listeners)
        {
            return JsonSerializer.Serialize(listeners.ToDictionary(x => x.Key.ToString(), y => y.Value.ToDictionary(y => y.Key.ToString(), y => y.Value)));
        }

        public async Task<Dictionary<long, Dictionary<long, string>>> GetListenersAsync(Dictionary<long, Dictionary<long, string>> lastListeners, Requester requester, CancellationToken token)
        {
            DateTime start = DateTime.Now;

            while (DateTime.Now - start < systemConfig.ListenTimeout)
            {
                //It seems strange to update the decayer with the WHOLE LIST but remember: this whole list is the EXACT SNAPSHOT of who's listening 
                //RIGHT NOW! because it's "instant", it's ok to continuously update the decayer, when someone leaves, they will appear gone in 
                //EVERYONE'S listener list connection
                listenDecayer.UpdateList(provider.Listeners.Where(x => x.ListenerId is RelationListener).Select(x => (RelationListener)x.ListenerId));

                //Decay the list only once, get the new list of listeners
                var listenersNow = listenDecayer.DecayList(systemConfig.ListenGracePeriod);

                //Creates a dictionary with pre-initialized keys. The keys won't change, we can keep redoing them.
                //This should be OK, considering we'll be creating new dictionaries every time anyway. As always, watch the CPU
                var result = lastListeners.ToDictionary(x => x.Key, y => new Dictionary<long, string>());

                //Assume the new listeners are appended to the end, which means they should be the newest and their statuses should override earlier ones... we hope.
                foreach(var listener in listenersNow)
                {
                    //Look over all PERTINENT statuses (only the ones that will end up in result)
                    foreach(var statusPair in listener.listenStatuses.Where(x => result.ContainsKey(x.Key)))
                        result[statusPair.Key][listener.userId] = statusPair.Value;
                        //if(!result[statusPair.Key].TryAdd(listener.userId, statusPair.Value))
                }

                if (result.Any(x => !x.Value.RealEqual(lastListeners[x.Key])))
                    return result;

                await Task.Delay(config.ListenerPollingInterval, token);
                token.ThrowIfCancellationRequested();
            }

            throw new TimeoutException("Ran out of time waiting for listeners");
        }

        public async Task<List<EntityRelation>> ListenAsync(RelationListenConfig listenConfig, Requester requester, CancellationToken token)
        {
            var listenId = new RelationListener() 
            { 
                userId = requester.userId,
                listenStatuses = listenConfig.statuses
            } ;

            var maxId = await provider.GetQueryable<EntityRelation>().MaxAsync(x => x.id);

            if(listenConfig.lastId < 0)
                listenConfig.lastId = maxId;
            else if(maxId - listenConfig.lastId > 1000)
                throw new BadRequestException("LastID too far back! Perhaps restart your listener!");

            //var entrances = 0;

            var results = await provider.ListenAsync<EntityRelation>(listenId, (q) => 
                q.Where(x => 
                    //Watches are special: we should get new ones and changes no matter WHAT the last id was! 
                    //(so long as it's the second runthrough)
                    (x.type == Keys.CommentHack || 
                        x.type == Keys.WatchRelation ||
                        x.type == Keys.WatchUpdate ||
                        x.type == Keys.WatchDelete ||
                        EF.Functions.Like(x.type, $"{Keys.ActivityKey}%") || 
                        EF.Functions.Like(x.type, $"{Keys.CommentDeleteHack}%") ||
                        EF.Functions.Like(x.type, $"{Keys.CommentHistoryHack}%")) && 
                       x.id > listenConfig.lastId),
            systemConfig.ListenTimeout, token);

            return results; 
        }
    }
}