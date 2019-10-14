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
            return (model.baseAllow & action) != 0 || (user != null && model.AccessList.Any(x => x.userId == user.entityId && (x.allow & action) != 0));
            //(model.baseAllow != null && model.baseAccess.Contains(doKey)) || (user != null && model.AccessList.Any(x => x.userId == user.id && x.access.Contains(doKey)));
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
                    baseAction = baseAction | mapping.Key;
                    access = access.Replace(mapping.Value, "");
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

        public void FillEntityAccess(EntityChild entity, EntityView view)
        {
            entity.Entity.baseAllow = StringToAccess(view.baseAccess);
            entity.Entity.AccessList = view.accessList.Select(x => new EntityAccess()
            {
                id = 0,
                userId = x.Key,
                createDate = DateTime.Now,
                allow = StringToAccess(x.Value)
            }).ToList();
        }

        //public bool CheckAccessFormat(string access)
        //{
        //    //Why do this manually? idk...
        //    Dictionary<char, int> counts = new Dictionary<char, int>();

        //    foreach(var character in access)
        //    {
        //        if(character != CreateChar && character != ReadChar && character != UpdateChar && character != DeleteChar)
        //            return false;
        //        if(!counts.ContainsKey(character))
        //            counts.Add(character, 0);
        //        if(++counts[character] > 1)
        //            return false;
        //    }

        //    return true;
        //}

        //public bool CheckAccessFormat(EntityView accessView)
        //{
        //    return (CheckAccessFormat(accessView.baseAccess) && accessView.accessList.All(x => CheckAccessFormat(x.Value)));
        //}
    }
}