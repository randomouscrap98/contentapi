using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using contentapi.Models;

namespace contentapi.Services
{
    public class AccessService
    {
        public Dictionary<EntityAction, string> ActionMapping = new Dictionary<EntityAction, string>()
        {
            { EntityAction.Create, "C" },
            { EntityAction.Read, "R"},
            { EntityAction.Update, "U"},
            { EntityAction.Delete, "D"}
        };

        public bool CanDo(Entity model, User user, EntityAction action)
        {
            return (model.baseAllow & action) != 0 || (user != null && model.AccessList != null && model.AccessList.Any(x => x.userId == user.entityId && (x.allow & action) != 0));
        }

        public bool CanCreate(Entity model, User user) { return CanDo(model, user, EntityAction.Create); }
        public bool CanRead(Entity model, User user) { return CanDo(model, user, EntityAction.Read); }
        public bool CanUpdate(Entity model, User user) { return CanDo(model, user, EntityAction.Update); }
        public bool CanDelete(Entity model, User user) { return CanDo(model, user, EntityAction.Delete); }

        public EntityAction StringToAccess(string access)
        {
            EntityAction baseAction = EntityAction.None;

            foreach(var mapping in ActionMapping)
            {
                if(access.Contains(mapping.Value))
                {
                    var length = access.Length;
                    baseAction = baseAction | mapping.Key;
                    access = access.Replace(mapping.Value, "");
                    if(length - access.Length > mapping.Value.Length)
                        throw new InvalidOperationException($"Malformed access string (no duplicates!)");
                }
            }

            if(!string.IsNullOrWhiteSpace(access))
                throw new InvalidOperationException($"Malformed access string ({string.Join("", ActionMapping.Values)})");

            return baseAction;
        }

        public string AccessToString(EntityAction action)
        {
            string result = "";

            foreach(var mapping in ActionMapping)
            {
                if((action & mapping.Key) != 0)
                    result += mapping.Value;
            }

            return result;
        }

        public void FillEntityAccess(Entity entity, EntityView view)
        {
            entity.baseAllow = StringToAccess(view.baseAccess);
            //NOTE: if you DON'T entirely recreate the list, you lose the "paper trail"!
            entity.AccessList = view.accessList.Select(x => new EntityAccess()
            {
                id = 0,
                userId = x.Key,
                createDate = DateTime.Now,
                allow = StringToAccess(x.Value)
            }).ToList();
            ////Remove all access lists that aren't in view
            //var viewUsers = view.accessList.Keys.ToList();
            //entity.AccessList.RemoveAll(x => !viewUsers.Contains(x.userId));
            ////Update access for things that ARE in the view
            ////Then add only the ones that AREN'T in the view
            //entity.AccessList.AddRange(view.accessList.Where(x => !entity.AccessList.Any(y => y.userId == x.Key)).Select(x => new EntityAccess()
            //{
            //    id = 0,
            //    userId = x.Key,
            //    createDate = DateTime.Now,
            //    allow = StringToAccess(x.Value)
            //}));
        }

        public void FillViewAccess(EntityView view, Entity entity)
        {
            view.baseAccess = AccessToString(entity.baseAllow);
            view.accessList = entity.AccessList.ToDictionary(k => k.userId, v => AccessToString(v.allow));
        }
    }
}