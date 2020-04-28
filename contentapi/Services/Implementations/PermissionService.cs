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

        public PermissionService(ILogger<PermissionService> logger, SystemConfig config, Keys keys)
        {
            this.keys = keys;
            this.logger = logger;
            this.config = config;
        }

        public IQueryable<E> PermissionWhere<E>(IQueryable<E> query, long user, string action, PermissionExtras extras = null) where E : EntityGroup
        {
            extras = extras ?? new PermissionExtras();

            bool superUser = config.SuperUsers.Contains(user);

            //Immediately apply a limiter so we're not joining on every dang relation ever (including comments etc).
            //The amount of creators and actions of a single type is SO MUCH LOWER. I'm not sure how optimized these
            //queries can get but better safe than sorry
            query = query.Where(x => x.permission.type == keys.CreatorRelation || x.permission.type == action);
            
            //Nothing else to do, the user can do it if it's update or delete.
            if(superUser && (action == keys.UpdateAction || action == keys.DeleteAction || action == keys.CreateAction))
                return query;

            return query.Where(x => 
                (extras.allowNegativeOwnerRelation && x.relation.entityId1 < 0) ||
                (user > 0 && x.permission.type == keys.CreatorRelation && x.permission.entityId1 == user) ||
                (x.permission.type == action && (x.permission.entityId1 == 0 || x.permission.entityId1 == user)));
        }

        public bool CanUser(long user, string action, EntityPackage package)
        {
            //Inefficient in compute but easier for me, the programmer, to use a single source of truth.
            return PermissionWhere(package.Relations.Select(x => new EntityGroup() { permission = x }).AsQueryable(), user, action).Any();
        }

        public bool IsSuper(long user)
        {
            return config.SuperUsers.Contains(user);
        }
    }
}