using System.Collections.Generic;
using System.Linq;
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

        public ViewServicePack(IEntityProvider provider, IMapper mapper, IPermissionService permissions, IHistoryService history)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.permissions = permissions;
            this.history = history;
        }
    }

    //The very most basic view service functions. Eventually, fix this to be services; don't have
    //time right now.
    public abstract class BaseViewServices<V,S> where S : BaseSearch //IConstrainedSearcher
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

            //This is the same, trust me (or it better be!). IDs are much faster
            if(search.Sort == "createdate")
                search.Sort = "id";
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
            
            var thing = 
                from v in provider.ApplyEntityValueSearch(provider.GetQueryable<EntityValue>(), valueSearch)
                join e in provider.GetQueryable<Entity>() on v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{type}%")
                select v;

            return (await provider.GetListAsync(thing)).OnlySingle();
        }

        public IQueryable<E> Q<E>() where E : EntityBase
        {
            return provider.GetQueryable<E>();
        }

        public void FailUnlessSuper(Requester requester)
        {
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Must be super user to perform this action!");
        }

        public async Task FixWatchLimits(WatchViewSource watchSource, Requester requester, IdLimiter limiter)
        {
            if(limiter.Watches)
            {
                var watchSearch = new WatchSearch();
                watchSearch.UserIds.Add(requester.userId);
                watchSearch.Reverse = true;

                limiter.Limit = (await watchSource.SimpleSearchAsync(watchSearch, q =>
                        services.permissions.PermissionWhere(q, requester, Keys.ReadAction)))
                    .Select(x => new IdLimit() { id = x.contentId, min = x.lastNotificationId }).ToList();

                // Just a silly thing to ensure "0" elements still means "no search" (although I hate that old
                // dicision... even though this one could easily be changed, consistency is better)
                limiter.Limit.Add(new IdLimit() { id = long.MaxValue }); 
            }
        }

    }
}