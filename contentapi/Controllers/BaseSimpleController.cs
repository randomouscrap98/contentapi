using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public class ControllerServices
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public Keys keys;
        public SystemConfig systemConfig;
        public IPermissionService permissions;
        public IActivityService activity;
        public IHistoryService history;

        public ControllerServices(IEntityProvider provider, IMapper mapper, Keys keys, SystemConfig systemConfig, 
            IPermissionService permissions, IActivityService activityService, IHistoryService history)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.keys = keys;
            this.systemConfig = systemConfig;
            this.permissions = permissions;
            this.activity = activityService;
            this.history = history;
        }
    }

    /// <summary>
    /// A bunch of methods extending the existing IProvider
    /// </summary>
    /// <remarks>
    /// Even though this extends from controller, it SHOULD NOT EVER use controller functions
    /// or fields or any of that. This is just a little silliness, I'm slapping stuff together.
    /// This is still testable without it being a controller though: please test sometime.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseSimpleController : ControllerBase
    {
        protected ControllerServices services;
        protected ILogger<BaseSimpleController> logger;
        
        protected Keys keys => services.keys;
        protected IEntityProvider provider => services.provider;

        public BaseSimpleController(ControllerServices services, ILogger<BaseSimpleController> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected long GetRequesterUid()
        {
            //Look for the UID from the JWT 
            var id = User.FindFirstValue(services.keys.UserIdentifier);

            if(id == null)
                throw new InvalidOperationException("User not logged in!");
            
            return long.Parse(id);
        }

        protected long GetRequesterUidNoFail()
        {
            try { return GetRequesterUid(); }
            catch { return -1; }
        }

        protected void FailUnlessRequestSuper()
        {
            if(!services.systemConfig.SuperUsers.Contains(GetRequesterUidNoFail()))
                throw new UnauthorizedAccessException("Must be super user to perform this action!");
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
        protected virtual E LimitSearch<E>(E search) where E : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }


        protected bool CanCurrentUser(string key, EntityPackage package)
        {
            return services.permissions.CanUser(GetRequesterUidNoFail(), key, package);
        }

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntitySearch search, Expression<Func<Entity, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntitySearch(Q<Entity>(), search, false)
                .Join(Q<EntityRelation>(), selector, r => r.entityId2, 
                (e,r) => new EntityGroup() { entity = e, permission = r});

            query = services.permissions.PermissionWhere(query, user, keys.ReadAction, extras);

            return query;
        }

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntityRelationSearch search, Expression<Func<EntityRelation, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntityRelationSearch(Q<EntityRelation>(), search, false)
                .Join(Q<EntityRelation>(), selector, r2 => r2.entityId2, 
                (r, r2) => new EntityGroup() { relation = r, permission = r2});

            query = services.permissions.PermissionWhere(query, user, keys.ReadAction, extras);

            return query;
        }

        /// <summary>
        /// Given a completed IQueryable, apply the final touches to get a real list of entities
        /// </summary>
        /// <param name="foundEntities"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        protected IQueryable<E> FinalizeQuery<E>(IQueryable<EntityGroup> groups, Expression<Func<EntityGroup, long>> groupId, EntitySearchBase search) where E : EntityBase
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

        //protected IQueryable<E> FinalizeQuery<E>(IQueryable<EntityGroup> groups, Expression<Func<EntityGroup, E>> groupId, EntitySearchBase search) where E : EntityBase
        //{
        //    //Group givens by grouping id and select only the grouped ID (all databases can do this)
        //    return provider.ApplyFinal(groups.GroupBy(groupId).Select(x => x.Key), search);
        //}

        protected IQueryable<EntityGroup> WhereParents(IQueryable<EntityGroup> query, List<long> parentIds)
        {
            return query
                .Join(Q<EntityRelation>(), e => e.entity.id, r => r.entityId2, 
                        (e,r) => new EntityGroup() { entity = e.entity, relation = r, permission = e.permission })
                .Where(x => x.relation.type == keys.ParentRelation && parentIds.Contains(x.relation.entityId1));
        }

        protected async Task<ActionResult<T>> ThrowToAction<T>(Func<Task<T>> action)
        {
            try
            {
                //Go find the parent. If it's not content, BAD BAD BAD
                return await action();
            }
            catch(AuthorizationException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
            catch(TimeoutException)
            {
                Response.Headers.Add("SBS-Warning", "Non-critical timeout");
                return null;
                //return StatusCode(408);
            }
            catch(OperationCanceledException)
            {
                logger.LogWarning("Pageload(?) got cancelled");
                return NoContent();
            }
        }

        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected async Task<EntityValue> FindValueAsync(string type, string key, string value = null, long id = -1) //long.MinValue)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            
            var thing = 
                from v in provider.ApplyEntityValueSearch(provider.GetQueryable<EntityValue>(), valueSearch)
                //where EF.Functions.Like(v.key, key) && (value == null || EF.Functions.Like(v.value, value)) && (id == long.MinValue || v.entityId == id)
                join e in provider.GetQueryable<Entity>() on v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{type}%")
                select v;

            return (await provider.GetListAsync(thing)).OnlySingle();
            //).OnlySingle();
        }

        protected IQueryable<E> Q<E>() where E : EntityBase
        {
            return provider.GetQueryable<E>();
        }
    }
}