using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        protected Keys keys;
        protected SystemConfig config;
        protected ILogger<PermissionService> logger;

        public List<long> SuperUsers => new List<long>(config.SuperUsers);
        public Dictionary<string, string> PermissionActionMap => new Dictionary<string, string>(permMapping);

        protected Dictionary<string, string> permMapping;

        public PermissionService(ILogger<PermissionService> logger, SystemConfig config, Keys keys)
        {
            this.keys = keys;
            this.logger = logger;
            this.config = config;

            //Static for now, not going to config just for this. It'll probably never change.
            permMapping = new Dictionary<string, string>()
            {
                {"c", keys.CreateAction},
                {"r", keys.ReadAction},
                {"u", keys.UpdateAction},
                {"d", keys.DeleteAction}
            };
        }

        public IQueryable<E> PermissionWhere<E>(IQueryable<E> query, Requester requester, string action, PermissionExtras extras = null) where E : EntityGroup
        {
            extras = extras ?? new PermissionExtras();

            //Immediately apply a limiter so we're not joining on every dang relation ever (including comments etc).
            //The amount of creators and actions of a single type is SO MUCH LOWER. I'm not sure how optimized these
            //queries can get but better safe than sorry
            query = query.Where(x => x.permission.type == keys.CreatorRelation || x.permission.type == action);
            
            //Nothing else to do, the user can do it if it's update or delete.
            if(requester.system || IsSuper(requester) && (action == keys.UpdateAction || action == keys.DeleteAction || action == keys.CreateAction))
                return query;

            var user = requester.userId;

            return query.Where(x => 
                (extras.allowNegativeOwnerRelation && x.relation.entityId1 < 0) ||
                (user > 0 && x.permission.type == keys.CreatorRelation && x.permission.entityId1 == user) ||
                (x.permission.type == action && (x.permission.entityId1 == 0 || x.permission.entityId1 == user)));
        }

        public bool CanUser(Requester requester, string action, EntityPackage package)
        {
            //Inefficient in compute but easier for me, the programmer, to use a single source of truth.
            return PermissionWhere(package.Relations.Select(x => new EntityGroup() { permission = x }).AsQueryable(), requester, action).Any();
        }

        public bool CanUser(long userId, string action, EntityPackage package)
        {
            return CanUser(new Requester() {userId = userId}, action, package);
        }

        public bool IsSuper(Requester requester)
        {
            return requester.system || config.SuperUsers.Contains(requester.userId);
        }

        public bool IsSuper(long userId)
        {
            return IsSuper(new Requester() {userId = userId});
        }

        public List<EntityRelation> ConvertPermsToRelations(Dictionary<string, string> perms)
        {
            var result = new List<EntityRelation>();
            foreach(var perm in perms)
            {
                foreach(var p in perm.Value.ToLower().Distinct().Select(x => x.ToString()))
                {
                    if(!permMapping.ContainsKey(p))
                        throw new InvalidOperationException("Bad character in permission");
                    
                    long userId = 0;

                    if(!long.TryParse(perm.Key, out userId))
                        throw new InvalidOperationException("Id not an integer!");

                    result.Add(new EntityRelation()
                    {
                        entityId1 = userId,
                        type = permMapping[p]
                    });
                }
            }
            return result;
        }

        public Dictionary<string, string> ConvertRelationsToPerms(IEnumerable<EntityRelation> relations)
        {
            var result = new Dictionary<string, string>();
            foreach(var relation in relations)
            {
                var perm = permMapping.Where(x => x.Value == relation.type);
                if(perm.Count() != 1)
                    continue;
                var userKey = relation.entityId1.ToString();
                if(!result.ContainsKey(userKey))
                    result.Add(userKey, "");
                result[userKey] += (perm.First().Key);
            }
            return result;
        }

        public void CheckPermissionValues(Dictionary<string, string> perms)
        {
            foreach(var perm in perms)
            {
                if(perm.Value.ToLower().Any(x => !permMapping.Keys.Contains(x.ToString())))
                    throw new BadRequestException($"Invalid characters in permission: {perm.Value}");
            }
        }

    }
}