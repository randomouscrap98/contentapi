using System;
using System.Collections.Generic;
using System.Linq;
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

        protected class EntityRGroup { public EntityRelation relation; }
        protected class EntityRRGroup : EntityRGroup { public EntityRelation relation2; }
        protected class EntityREGroup : EntityRGroup { public Entity entity; }
        protected class EntityREVGroup : EntityREGroup { public EntityValue value; }

        protected IQueryable<E> PermissionWhere<E>(IQueryable<E> query, long user, string action) where E : EntityRGroup
        {
            return query.Where(x => (x.relation.type == keys.CreatorRelation && x.relation.entityId1 == user) ||
                (x.relation.type == action && (x.relation.entityId1 == 0 || x.relation.entityId1 == user)));
        }

        protected bool CanUser(long user, string action, EntityPackage package)
        {
            //Inefficient in compute but easier for me, the programmer, to use a single source of truth.
            return PermissionWhere(package.Relations.Select(x => new EntityRGroup() { relation = x }).AsQueryable(), user, action).Any();
        }

        protected bool CanCurrentUser(string key, EntityPackage package)
        {
            return CanUser(GetRequesterUidNoFail(), key, package);
        }


        protected IQueryable<EntityREGroup> BasicPermissionQuery(long user, EntitySearch search)
        {
            var query = provider.ApplyEntitySearch(provider.GetQueryable<Entity>(), search, false)
                .Join(provider.GetQueryable<EntityRelation>(), e => e.id, r => r.entityId2, (e,r) => new EntityREGroup() { entity = e, relation = r});
            
            query = PermissionWhere(query, user, keys.ReadAction);

            return query;
        }

        /// <summary>
        /// Given a completed IQueryable, apply the final touches to get a real list of entities
        /// </summary>
        /// <param name="foundEntities"></param>
        /// <param name="search"></param>
        /// <returns></returns>
        protected IQueryable<E> FinalizeHusk<E>(IQueryable<EntityBase> foundEntities, EntitySearchBase search) where E : EntityBase
        {
            var ids = provider.ApplyFinal(foundEntities, search).Select(x => x.id);
            var join =
                from e in provider.GetQueryable<E>()
                join i in ids on e.id equals i
                select e;

            ////This is REPEAT CODE! FIGURE OUT HOW TO FIX THIS! This is required because order is not preserved
            ////after the "join" (the fake join using in-memory data oof)
            //if(search.Reverse)
            //    join = join.OrderByDescending(x => x.id);
            //else
            //    join = join.OrderBy(x => x.id);

            return join;
        }

        protected IQueryable<EntityREGroup> WhereParents(IQueryable<EntityREGroup> query, List<long> parentIds)
        {
            return query
                .Join(provider.GetQueryable<EntityRelation>(), e => e.entity.id, r => r.entityId2, 
                        (e,r) => new EntityREGroup() { entity = e.entity, relation = r})
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
            catch(TaskCanceledException)
            {
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