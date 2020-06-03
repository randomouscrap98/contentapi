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

    public class RelationListenerServiceConfig
    {
        public TimeSpan ListenerPollingInterval = TimeSpan.FromSeconds(2);
    }

    //When registering this, make it a singleton
    public class RelationListenerService
    {
        protected IDecayer<RelationListener> listenDecayer = null;
        //protected static readonly object listenDecayLock = new object();
        protected IEntityProvider provider;
        protected SystemConfig systemConfig;
        protected RelationListenerServiceConfig config;
        protected ILogger logger;


        //Don't know what to do with this yet...
        //protected IEnumerable<string> relationTypes = new[] { Keys.CommentHack, Keys.CommentDeleteHack, Keys.CommentHistoryHack };

        public RelationListenerService(ILogger<RelationListenerService> logger, IDecayer<RelationListener> decayer,
            IEntityProvider provider, SystemConfig systemConfig, RelationListenerServiceConfig config)
        {
            this.logger = logger;
            this.provider = provider;
            this.systemConfig = systemConfig;
            this.config = config;

            //lock(listenDecayLock)
            //{
                //if (listenDecayer == null)
            this.listenDecayer = decayer;
            //}
        }

        public string FormatListeners(Dictionary<long,Dictionary<long,string>> listeners)
        {
            return JsonSerializer.Serialize(listeners.ToDictionary(x => x.Key.ToString(), y => y.Value.ToDictionary(y => y.Key.ToString(), y => y.Value)));
            //return string.Join(",", listeners.ToList().Select(x => x.Key + ": " + 
            //    string.Join("x.Value.Select(y => )));
            //var builder = new StringBuilder();

            //foreach(var kv in listeners)
            //{
            //    builder.Append($"{kv.Key}:");
            //}

            //return builder.ToString();
        }

        public async Task<Dictionary<long, Dictionary<long, string>>> GetListenersAsync(Dictionary<long, Dictionary<long, string>> lastListeners, Requester requester, CancellationToken token)
        {
            DateTime start = DateTime.Now;

            //Creates a dictionary with pre-initialized keys. The keys won't change, we can keep redoing them.
            var result = lastListeners.ToDictionary(x => x.Key, y => new Dictionary<long, string>());

            while (DateTime.Now - start < systemConfig.ListenTimeout)
            {
                //Update listeners every IDK, some small time (every time we check).
                //var listeners = GetListeners();

                //if(listeners.Any)
                listenDecayer.UpdateList(GetListeners());

                //This list won't change as we're polling, so it's safe to keep writing over the old stuff.
                foreach(var parentKey in result.Keys.ToList())
                    result[parentKey] = listenDecayer.DecayList(systemConfig.ListenGracePeriod).Where(x => x.listenStatuses.ContainsKey(parentKey)).ToDictionary(x => x.userId, y => y.listenStatuses[parentKey]);

                if (result.Any(x => !x.Value.RealEqual(lastListeners[x.Key])))
                    return result;
                
                //throw new InvalidOperationException($"SOMEHOW IT'S NOT WORKING!!! VALUES: {FormatListeners(result)} GIVEN: {FormatListeners(lastListeners)}"); //{string.Join(",", result.ToList().Select(x => $"{x.Key}:{x.v"))}");

                await Task.Delay(config.ListenerPollingInterval, token);
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

            //var entrances = 0;

            var results = await provider.ListenAsync<EntityRelation>(listenId, (q) => 
            //{
                //entrances++;

                q.Where(x => 
                    //Watches are special: we should get new ones and changes no matter WHAT the last id was! 
                    //(so long as it's the second runthrough)
                    //(x.type == Keys.WatchRelation && x.entityId1 == requester.userId && entrances > 1) ||
                    (x.type == Keys.CommentHack || 
                        x.type == Keys.WatchRelation ||
                        x.type == Keys.WatchUpdate ||
                        x.type == Keys.WatchDelete ||
                        EF.Functions.Like(x.type, $"{Keys.ActivityKey}%") || 
                        EF.Functions.Like(x.type, $"{Keys.CommentDeleteHack}%") ||
                        EF.Functions.Like(x.type, $"{Keys.CommentHistoryHack}%")) && 
                       x.id > listenConfig.lastId),

                //return query;
            //}, 
            systemConfig.ListenTimeout, token);

            return results; 
        }
    }
}