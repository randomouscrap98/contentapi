using System.Collections.Generic;
using System.Linq;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    public class PermissionExtras
    {
        public bool allowNegativeOwnerRelation = false;
    }

    public interface IPermissionService
    {
        List<long> SuperUsers {get;}

        bool IsSuper(long user);

        IQueryable<E> PermissionWhere<E>(IQueryable<E> query, long user, string action, PermissionExtras extras = null) where E : EntityGroup;
        bool CanUser(long user, string action, EntityPackage package);

    }
}