using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using contentapi.Configs;
using contentapi.Services.Constants;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        protected SystemConfig config;
        protected ILogger<PermissionService> logger;

        public List<long> SuperUsers => new List<long>(config.SuperUsers);

        public PermissionService(ILogger<PermissionService> logger, SystemConfig config)
        {
            this.logger = logger;
            this.config = config;
        }

        protected Expression<Func<E,bool>> PermissionWhereBasePrecomputed<E>(long user, bool isSuper, string action, PermissionExtras extras = null) where E : EntityGroup
        {
            extras = extras ?? new PermissionExtras();

            bool isRead = action == Keys.ReadAction;
            bool allowRelationTypes = extras.allowedRelationTypes.Count > 0;

            return x => 
                //Note: the "extras" is a hack: I need "OR" parameters on permissions but can't
                //just... do that. Until I get something better set up, this hack stuff is VEry particular and for
                //VERY specific fields (that might not be there for every request, only the ones with the flags set.)
                (!isRead && isSuper) ||
                (extras.allowNegativeOwnerRelation && x.relation.entityId1 < 0) ||
                (allowRelationTypes && extras.allowedRelationTypes.Contains(x.relation.type)) ||
                (user > 0 && x.permission.type == Keys.CreatorRelation && x.permission.entityId1 == user) ||
                (x.permission.type == action && (x.permission.entityId1 == 0 || x.permission.entityId1 == user));
        }

        public IQueryable<E> PermissionWhere<E>(IQueryable<E> query, Requester requester, string action, PermissionExtras extras = null) where E : EntityGroup
        {
            if(requester.system)
                return query;
            else
                return query.Where(PermissionWhereBasePrecomputed<E>(requester.userId, IsSuper(requester), action, extras));
        }

        public Dictionary<long, string> CanUserMany(Requester requester, IEnumerable<EntityPackage> contents)
        {
            bool isSuper = IsSuper(requester);
            long user = requester.userId;

            var result = new Dictionary<long, string>();
            var builder = new StringBuilder();
            var extras = new PermissionExtras();
            var permbase = Actions.ActionMap.Values.ToDictionary(x => x, y => 
                PermissionWhereBasePrecomputed<EntityGroup>(user, isSuper, y, extras).Compile());

            foreach(var c in contents)
            {
                builder.Clear();
                var queryon = c.Relations.Select(x => new EntityGroup() { permission = x });

                foreach(var action in Actions.ActionMap)
                {
                    if(queryon.Any(permbase[action.Value]))
                        builder.Append(action.Key);
                }

                result.Add(c.Entity.id, builder.ToString());
            }

            return result;
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


        public void CheckPermissionValues(Dictionary<long, string> perms)
        {
            foreach(var perm in perms)
            {
                if(perm.Value.ToLower().Any(x => !Actions.ActionMap.Keys.Contains(x.ToString())))
                    throw new BadRequestException($"Invalid characters in permission: {perm.Value}");
            }
        }

    }
}