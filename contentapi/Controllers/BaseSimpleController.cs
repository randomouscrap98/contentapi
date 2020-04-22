using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        public ControllerServices(IEntityProvider provider, IMapper mapper, Keys keys, IOptionsMonitor<SystemConfig> systemConfig)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.keys = keys;
            this.systemConfig = systemConfig.CurrentValue;
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

        protected class EntityGroup
        { 
            public Entity entity;
            public EntityRelation relation;
            public EntityValue value;
            public EntityRelation permission; 
        }

        public class PermissionExtras
        {
            public bool allowNegativeOwnerRelation = false;
        }

        protected IQueryable<E> PermissionWhere<E>(IQueryable<E> query, long user, string action, PermissionExtras extras = null) where E : EntityGroup
        {
            extras = extras ?? new PermissionExtras();

            bool superUser = services.systemConfig.SuperUsers.Contains(user);

            //Immediately apply a limiter so we're not joining on every dang relation ever (including comments etc).
            //The amount of creators and actions of a single type is SO MUCH LOWER. I'm not sure how optimized these
            //queries can get but better safe than sorry
            query = query.Where(x => x.permission.type == keys.CreatorRelation || x.permission.type == action);
            
            //Nothing else to do, the user can do it if it's update or delete.
            if(superUser && (action == keys.UpdateAction || action == keys.DeleteAction))
                return query;

            return query.Where(x => 
                (extras.allowNegativeOwnerRelation && x.relation.entityId1 < 0) ||
                (user > 0 && x.permission.type == keys.CreatorRelation && x.permission.entityId1 == user) ||
                (x.permission.type == action && (x.permission.entityId1 == 0 || x.permission.entityId1 == user)));
        }

        //These are here because they directly use PermissionWhere
        protected bool CanUser(long user, string action, EntityPackage package)
        {
            //Inefficient in compute but easier for me, the programmer, to use a single source of truth.
            return PermissionWhere(package.Relations.Select(x => new EntityGroup() { permission = x }).AsQueryable(), user, action).Any();
        }

        protected bool CanCurrentUser(string key, EntityPackage package)
        {
            return CanUser(GetRequesterUidNoFail(), key, package);
        }

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntitySearch search, Expression<Func<Entity, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntitySearch(provider.GetQueryable<Entity>(), search, false)
                .Join(provider.GetQueryable<EntityRelation>(), selector, r => r.entityId2, 
                (e,r) => new EntityGroup() { entity = e, permission = r});

            query = PermissionWhere(query, user, keys.ReadAction, extras);

            return query;
        }

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntityRelationSearch search, Expression<Func<EntityRelation, long>> selector, PermissionExtras extras = null)
        {
            var query = provider.ApplyEntityRelationSearch(provider.GetQueryable<EntityRelation>(), search, false)
                .Join(provider.GetQueryable<EntityRelation>(), selector, r2 => r2.entityId2, 
                (r, r2) => new EntityGroup() { relation = r, permission = r2});

            query = PermissionWhere(query, user, keys.ReadAction, extras);

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

        protected IQueryable<EntityGroup> WhereParents(IQueryable<EntityGroup> query, List<long> parentIds)
        {
            return query
                .Join(provider.GetQueryable<EntityRelation>(), e => e.entity.id, r => r.entityId2, 
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
                return StatusCode(408);
            }
            catch(OperationCanceledException)
            {
                logger.LogWarning("Pageload(?) got cancelled");
                return NoContent();
            }
        }

        /// <summary>
        /// Link all given values and relations to the given parent (do not write it!)
        /// </summary>
        /// <param name="values"></param>
        /// <param name="relations"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        protected void Relink(IEnumerable<EntityValue> values, IEnumerable<EntityRelation> relations, Entity parent)
        {
            foreach(var v in values)
                v.entityId = parent.id;
            foreach(var r in relations)
                r.entityId2 = parent.id;
        }

        /// <summary>
        /// Assuming a valid base entity, relink all items in the package by id
        /// </summary>
        /// <param name="package"></param>
        protected void Relink(EntityPackage package)
        {
            Relink(package.Values, package.Relations, package.Entity);
        }

        protected void FlattenPackage(EntityPackage package, List<EntityBase> collection)
        {
            collection.AddRange(package.Values);
            collection.AddRange(package.Relations);
            collection.Add(package.Entity);
        }

        protected List<EntityBase> FlattenPackage(EntityPackage package)
        {
            var result = new List<EntityBase>();
            FlattenPackage(package, result);
            return result;
        }

    }
}