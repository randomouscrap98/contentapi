using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class RelationListenConfig
    {
        public long lastId {get;set;} = 0;

        /// <summary>
        /// What your "status" should be in each room (arbitrary string yes)
        /// </summary>
        /// <value></value>
        public Dictionary<long, string> statuses {get;set;} = new Dictionary<long, string>();
        public List<long> clearNotifications {get;set;} = new List<long>();
    }

    public class RelationListener
    {
        private static long GlobalListenId = 0;

        //Hopefully this is NICE AND FASTU
        public long listenerId = Interlocked.Increment(ref GlobalListenId);
        public long userId {get;set;}

        //this is a mapping of contentId to personal status in content
        public Dictionary<long, string> listenStatuses {get;set;} = new Dictionary<long, string>();

        public override bool Equals(object obj)
        {
            //This is essentially the same as object reference checking: two things are only equal if they
            //refer to the SAME OBJECT! I'm only doing it like because ugh idk, just in case I need something else.
            if(obj != null && obj is RelationListener)
                return ((RelationListener)obj).listenerId == listenerId;

            return false;
        }

        public override int GetHashCode()
        {
            return listenerId.GetHashCode(); //userId.GetHashCode();
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
        protected UserViewService userService;
        protected ILogger logger;


        public RelationListenerService(ILogger<RelationListenerService> logger, IDecayer<RelationListener> decayer,
            IEntityProvider provider, SystemConfig systemConfig, RelationListenerServiceConfig config,
            UserViewService userService)
        {
            this.logger = logger;
            this.provider = provider;
            this.systemConfig = systemConfig;
            this.config = config;
            this.userService = userService;

            this.listenDecayer = decayer;
        }

        public string FormatListeners(Dictionary<long,Dictionary<long,string>> listeners)
        {
            return JsonSerializer.Serialize(listeners.ToDictionary(x => x.Key.ToString(), y => y.Value.ToDictionary(y => y.Key.ToString(), y => y.Value)));
        }

        public IEnumerable<RelationListener> GetInstantListeners(IEnumerable<long> parentIds = null)
        {
            var allListeners = provider.Listeners.Where(x => x.ListenerId is RelationListener).Select(x => (RelationListener)x.ListenerId);

            if(parentIds != null)
                allListeners = allListeners.Where(x => x.listenStatuses.Any(y => parentIds.Contains(y.Key)));
            
            return allListeners;
        }

        public Dictionary<long, Dictionary<long, string>> GetListenersAsDictionary(IEnumerable<RelationListener> listeners, IEnumerable<long> parentIds)
        {
            var result = parentIds.ToDictionary(x => x, y => new Dictionary<long, string>());

            //Assume the new listeners are appended to the end, which means they should be the newest and their statuses should override earlier ones... we hope.
            foreach (var listener in listeners)
            {
                //Look over all PERTINENT statuses (only the ones that will end up in result). PICK THE HIGHEST BY ALPHABETICAL ORDER!
                foreach (var statusPair in listener.listenStatuses.Where(x => result.ContainsKey(x.Key)))
                {
                    //Only add if it doesn't exist OR if the new value (statusPair.Value) comes AFTER the one we gave alphabetically
                    if(!result[statusPair.Key].ContainsKey(listener.userId) || statusPair.Value.CompareTo(result[statusPair.Key][listener.userId]) > 0)
                        result[statusPair.Key][listener.userId] = statusPair.Value;
                }
            }

            return result;
        }

        public async Task<Dictionary<long, Dictionary<long, string>>> GetListenersAsync(
            Dictionary<long, Dictionary<long, string>> lastListeners, Requester requester, CancellationToken token)
        {
            DateTime start = DateTime.Now;

            while (DateTime.Now - start < systemConfig.ListenTimeout)
            {
                //It seems strange to update the decayer with the WHOLE LIST but remember: this whole list is the EXACT SNAPSHOT of who's listening 
                //RIGHT NOW! because it's "instant", it's ok to continuously update the decayer, when someone leaves, they will appear gone in 
                //EVERYONE'S listener list connection
                listenDecayer.UpdateList(GetInstantListeners());

                //Decay the list only once, get the new list of listeners
                var listenersNow = listenDecayer.DecayList(systemConfig.ListenGracePeriod);

                //Creates a dictionary with pre-initialized keys. The keys won't change, we can keep redoing them.
                //This should be OK, considering we'll be creating new dictionaries every time anyway. As always, watch the CPU
                var result = GetListenersAsDictionary(listenersNow, lastListeners.Keys);

                var users = result.Values.SelectMany(x => x.Keys).ToList();

                //foreach(var hideval in await provider.GetEntityValuesAsync(new EntityValueSearch() { EntityIds = users, KeyLike = Keys.UserHideKey }))
                foreach(var hideval in (await userService.GetUserHideDataAsync(users)).hides)
                {
                    //Parse the actual hide values for this user.
                    //var hides = hideval.value.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x));

                    //Now loop through the rooms and completely remove users from lists if they're hiding there
                    foreach(var room in result)
                    {
                        if(hideval.hides.Contains(0) || hideval.hides.Contains(room.Key))
                            room.Value.Remove(hideval.userId);
                    }
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

            var maxId = await provider.GetMaxAsync(await provider.GetQueryableAsync<EntityRelation>(), x => x.id);

            if(listenConfig.lastId <= 0)
                listenConfig.lastId = maxId + listenConfig.lastId; //plus because negative
            else if(maxId - listenConfig.lastId > 1000)
                throw new BadRequestException($"LastID too far back! Perhaps restart your listener! System current max: {maxId}");

            //This SERIOUSLY requires INTIMATE knowledge of how each of these systems works, like what entityId1 means etc.
            //That's bad.
            var results = await provider.ListenAsync<EntityRelation>(listenId, (q) => 
                q.Where(x => 
                    (x.type == Keys.CommentHack || 
                     x.type == Keys.WatchRelation && x.entityId1 == listenId.userId ||
                     (x.type == Keys.WatchUpdate ||
                      x.type == Keys.WatchDelete) && x.value.StartsWith($"{listenId.userId}_") ||
                     x.type.StartsWith(Keys.ActivityKey) || 
                     x.type.StartsWith(Keys.ModuleMessageKey) && (x.entityId2 == 0 || x.entityId2 == -requester.userId) ||
                     x.type.StartsWith(Keys.CommentDeleteHack) ||
                     x.type.StartsWith(Keys.CommentHistoryHack)) && 
                    x.id > listenConfig.lastId),
            systemConfig.ListenTimeout, token);

            return results; 
        }
    }
}