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

        bool IsSuper(Requester requester);
        bool IsSuper(long userId);

        IQueryable<E> PermissionWhere<E>(IQueryable<E> query, Requester requester, string action, PermissionExtras extras = null) where E : EntityGroup;
        bool CanUser(Requester requester, string action, EntityPackage package);

        void CheckPermissionValues(Dictionary<string, string> perms);
    }
}