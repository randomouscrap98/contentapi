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
    public abstract class SimpleBaseController : ControllerBase
    {
        protected ControllerServices services;
        protected ILogger<SimpleBaseController> logger;
        
        protected Keys keys => services.keys;

        public SimpleBaseController(ControllerServices services, ILogger<SimpleBaseController> logger)
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

        //Parameters are like reading: is x y
        protected bool TypeIs(string given, string expected)
        {
            if(given == null)
                return false;

            return given.StartsWith(expected);
        }

        //Parameters are like reading: set x to y
        protected string TypeSet(string existing, string type)
        {
            return type + (existing ?? "");
        }

        protected string TypeSub(string given, string mainType)
        {
            return given.Substring(mainType.Length);
        }

        //protected 

        /// <summary>
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        protected virtual E LimitSearch<E>(E search) where E : EntitySearchBase//where S : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        protected class EntityRGroup { public EntityRelation relation; }
        protected class EntityRRGroup : EntityRGroup { public EntityRelation relation2; }
        protected class EntityREGroup : EntityRGroup { public Entity entity; }
        protected class EntityREVGroup : EntityREGroup { public EntityValue value; }

        protected IQueryable<E> PermissionWhere<E>(IQueryable<E> query, long user) where E : EntityRGroup
        {
            return query.Where(x => (x.relation.type == keys.CreatorRelation && x.relation.entityId1 == user) ||
                (x.relation.type == keys.ReadAction && (x.relation.entityId1 == 0 || x.relation.entityId1 == user)));
        }

        protected IQueryable<EntityREGroup> BasicPermissionQuery(long user, EntitySearch search)
        {
            var query = services.provider.ApplyEntitySearch(services.provider.GetQueryable<Entity>(), search, false)
                .Join(services.provider.GetQueryable<EntityRelation>(), e => e.id, r => r.entityId2, (e,r) => new EntityREGroup() { entity = e, relation = r});
            
            query = PermissionWhere(query, user);

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
            var ids = services.provider.ApplyFinal(foundEntities, search).Select(x => x.id);
            var join =
                from e in services.provider.GetQueryable<E>()
                where ids.Contains(e.id)
                select e;

            //This is REPEAT CODE! FIGURE OUT HOW TO FIX THIS! This is required because order is not preserved
            //after the "join" (the fake join using in-memory data oof)
            if(search.Reverse)
                join = join.OrderByDescending(x => x.id);
            else
                join = join.OrderBy(x => x.id);

            return join;
        }

        /// <summary>
        /// Convert stand-in ids (from the users) to real ids (that I use for searching actual entities)
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        protected async Task<List<long>> ConvertStandInIdsAsync(List<long> ids)
        {
            //This bites me every time. I need to fix the entity search system.
            if(ids.Count == 0)
                return new List<long>();

            var relations = services.provider.GetQueryable<EntityRelation>();
            relations = services.provider.ApplyEntityRelationSearch(relations, 
                new EntityRelationSearch()
                {
                    EntityIds1 = ids,
                    TypeLike = keys.StandInRelation
                });

            return await services.provider.GetListAsync(relations.Where(x => EF.Functions.Like(x.value, $"{keys.ActiveValue}%")).Select(x => x.entityId2));
        }

        protected Task<List<long>> ConvertStandInIdsAsync(params long[] ids)
        {
            return ConvertStandInIdsAsync(ids.ToList());
        }
            
        /// <summary>
        /// Find an entity by its STAND IN id (not the regular ID, use the service for that)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<EntityPackage> FindByIdAsync(long standinId)
        {
            var realIds = await ConvertStandInIdsAsync(standinId);

            if(realIds.Count() == 0)
                return null;
            else if(realIds.Count() > 1)
                throw new InvalidOperationException("Multiple entities for given standin, are there trailing history elements?");
            
            return await services.provider.FindByIdAsync(realIds.First());
        }

        protected bool CanUser(long user, string key, EntityPackage package)
        {
            return (package.GetRelation(keys.CreatorRelation).entityId1 == user) ||
                (package.Relations.Any(x => x.type == key && (x.entityId1 == user || x.entityId1 == 0)));
        }

        protected bool CanCurrentUser(string key, EntityPackage package)
        {
            return CanUser(GetRequesterUidNoFail(), key, package);
        }

        protected async Task<ActionResult<T>> ThrowToAction<T>(Func<Task> checkAction, Func<Task<T>> resultAction)
        {
            try
            {
                //Go find the parent. If it's not content, BAD BAD BAD
                await checkAction();
            }
            catch(AuthorizationException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }

            return await resultAction();
        }
    }
}