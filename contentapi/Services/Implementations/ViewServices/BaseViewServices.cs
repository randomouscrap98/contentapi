using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ViewServicePack 
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public Keys keys;
        public IPermissionService permissions;
        public IHistoryService history;

        public ViewServicePack(IEntityProvider provider, IMapper mapper, Keys keys, IPermissionService permissions, 
            IHistoryService history)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.keys = keys;
            this.permissions = permissions;
            this.history = history;
        }
    }

    //The very most basic view service functions. Eventually, fix this to be services; don't have
    //time right now.
    public class BaseViewServices
    {
        protected ViewServicePack services;
        protected ILogger logger;
        
        protected Keys keys => services.keys;
        protected IEntityProvider provider => services.provider;

        public BaseViewServices(ViewServicePack services, ILogger<BaseViewServices> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected EntityPackage NewEntity(string name, string content = null)
        {
            return new EntityPackage()
            {
                Entity = new Entity() { 
                    name = name, 
                    content = content ,
                    createDate = DateTime.UtcNow
                }
            };
        }

        protected EntityValue NewValue(string key, string value)
        {
            return new EntityValue() 
            {
                key = key, 
                value = value, 
                createDate = null 
            };
        }

        protected EntityRelation NewRelation(long parent, string type, string value = null)
        {
            return new EntityRelation()
            {
                entityId1 = parent,
                type = type,
                value = value,
                createDate = null
            };
        }

        /// <summary>
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        public virtual E LimitSearch<E>(E search) where E : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        public IQueryable<EntityGroup> BasicReadQuery(Requester requester, EntitySearch search, Expression<Func<Entity, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntitySearch(Q<Entity>(), search, false)
                .Join(Q<EntityRelation>(), selector, r => r.entityId2, 
                (e,r) => new EntityGroup() { entity = e, permission = r});

            query = services.permissions.PermissionWhere(query, requester, keys.ReadAction, extras);

            return query;
        }

        public IQueryable<EntityGroup> BasicReadQuery(Requester requester, EntityRelationSearch search, Expression<Func<EntityRelation, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntityRelationSearch(Q<EntityRelation>(), search, false)
                .Join(Q<EntityRelation>(), selector, r2 => r2.entityId2, 
                (r, r2) => new EntityGroup() { relation = r, permission = r2});

            query = services.permissions.PermissionWhere(query, requester, keys.ReadAction, extras);

            return query;
        }

        /// <summary>
        /// Given a completed IQueryable, apply the final touches to get a real list of entities
        /// </summary>
        /// <param name="foundEntities"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        public IQueryable<E> FinalizeQuery<E>(IQueryable<EntityGroup> groups, Expression<Func<EntityGroup, long>> groupId, EntitySearchBase search) where E : EntityBase
        {
            //Group givens by grouping id and select only the grouped ID (all databases can do this)
            var husks = groups.GroupBy(groupId).Select(x => new EntityBase() { id = x.Key });

            //Apply the final search parameters on the RESULT (that would be ordering/limiting/etc)
            var ids = provider.ApplyFinal(husks, search).Select(x => x.id);

            //Join the ids with the actual table you want to get the final product (since grouping doesn't persist... ugh)
            var join =
                from e in provider.GetQueryable<E>()
                join i in ids on e.id equals i
                select e;

            return join;
        }

        public IQueryable<EntityGroup> WhereParents(IQueryable<EntityGroup> query, List<long> parentIds)
        {
            return query
                .Join(Q<EntityRelation>(), e => e.entity.id, r => r.entityId2, 
                        (e,r) => new EntityGroup() { entity = e.entity, relation = r, permission = e.permission })
                .Where(x => x.relation.type == keys.ParentRelation && parentIds.Contains(x.relation.entityId1));
        }

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
            if(!services.permissions.IsSuper(requester)) //services.systemConfig.SuperUsers.Contains(GetRequesterUidNoFail()))
                throw new UnauthorizedAccessException("Must be super user to perform this action!");
        }
    }
}