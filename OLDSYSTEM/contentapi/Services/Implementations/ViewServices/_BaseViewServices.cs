using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ViewServicePack 
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public IPermissionService permissions;
        public IHistoryService history;
        public ICodeTimer timer;

        public ViewServicePack(IEntityProvider provider, IMapper mapper, IPermissionService permissions, IHistoryService history, ICodeTimer timer)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.permissions = permissions;
            this.history = history;
            this.timer = timer;
        }
    }

    //The very most basic view service functions. Eventually, fix this to be services; don't have
    //time right now.
    public abstract class BaseViewServices<V,S> where S : BaseSearch, new() //IConstrainedSearcher
    {
        protected ViewServicePack services;
        protected ILogger logger;
        
        protected IEntityProvider provider => services.provider;

        public BaseViewServices(ViewServicePack services, ILogger<BaseViewServices<V,S>> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        public virtual void LimitSearch(S search, Requester requester)
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            if(search.Sort != null)
                search.Sort = search.Sort.ToLower().Trim();
            
            if(search.Ids != null && search.Ids.Count > 0)
                search.Ids = search.Ids.Distinct().OrderBy(x => x).ToList();

            if(search.NotIds != null && search.NotIds.Count > 0)
                search.NotIds = search.NotIds.Distinct().OrderBy(x => x).ToList();

            //This is the same, trust me (or it better be!). IDs are much faster
            if(search.Sort == "createdate")
                search.Sort = "id";
        }

        /// <summary>
        /// Return whether the given search made by the given requester is ONLY ids. WARN: LIMITS SEARCH!
        /// </summary>
        /// <param name="search"></param>
        /// <param name="r"></param>
        /// <returns></returns>
        public bool OnlyIdSearch(S search, Requester r)
        {
            LimitSearch(search, r);
            var otherSearch = new S() { Ids = search.Ids };
            LimitSearch(otherSearch, r); //Apply the same rules!
            var otherSearchJson = JsonSerializer.Serialize(otherSearch);
            var searchJson = JsonSerializer.Serialize(search);

            //This means ONLY the ids are used!
            return otherSearchJson == searchJson;
        }

        public Task<List<V>> SearchAsync(S search, Requester requester)
        {
            LimitSearch(search, requester);

            //Can now ALSO track views here perhaps...

            return PreparedSearchAsync(search, requester);
        }

        public abstract Task<List<V>> PreparedSearchAsync(S search, Requester requester);

        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<EntityValue> FindValueAsync(string type, string key, string value = null, long id = -1) //long.MinValue)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            
            var values = await provider.GetQueryableAsync<EntityValue>();
            var entities = await provider.GetQueryableAsync<Entity>();

            var thing = 
                from v in provider.ApplyEntityValueSearch(values, valueSearch)
                join e in entities on v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{type}%")
                select v;

            return (await provider.GetListAsync(thing)).OnlySingle();
        }

        public Task<IQueryable<E>> Q<E>() where E : EntityBase
        {
            return provider.GetQueryableAsync<E>();
        }

        public void FailUnlessSuper(Requester requester)
        {
            if(!services.permissions.IsSuper(requester))
                throw new ForbiddenException("Must be super user to perform this action!");
        }

        /// <summary>
        /// Replace contentlimit list with watchlist (yes, FULL replacement!)
        /// </summary>
        /// <param name="watchSource"></param>
        /// <param name="requester"></param>
        /// <param name="limiter"></param>
        /// <returns>Whether or not the "limiter" has been properly limited by permission in this call</returns>
        public async Task<bool> FixWatchLimits(WatchViewSource watchSource, Requester requester, IdLimiter limiter)
        {
            if(limiter.Watches)
            {   
                if(requester.userId > 0)
                {
                    var watchSearch = new WatchSearch();
                    watchSearch.UserIds.Add(requester.userId);
                    watchSearch.Reverse = true;

                    limiter.Limit = (await watchSource.SimpleSearchAsync(watchSearch, q =>
                            services.permissions.PermissionWhere(q, requester, Keys.ReadAction)))
                        .Select(x => new IdLimit() { id = x.contentId, min = x.lastNotificationId }).ToList();

                    return true;
                }

                // Just a silly thing to ensure "0" elements still means "no search" (although I hate that old
                // dicision... even though this one could easily be changed, consistency is better)
                limiter.Limit.Add(new IdLimit() { id = long.MaxValue }); 
            }

            return false;
        }

    }
}