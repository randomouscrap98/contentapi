using System.Collections.Generic;
using System.Linq;
using contentapi.Models;

namespace contentapi.Services
{
    public class AccessService
    {
        public const char CreateChar = 'C';
        public const char ReadChar = 'R';
        public const char UpdateChar = 'U';
        public const char DeleteChar = 'D';

        protected bool CanDo(IGenericAccessModel model, User user, char doKey)
        {
            return model.baseAccess.Contains(doKey) || (user != null && model.GenericAccessList.Any(x => x.userId == user.id && x.access.Contains(doKey)));
        }

        public bool CanCreate(IGenericAccessModel model, User user) { return CanDo(model, user, CreateChar); }
        public bool CanRead(IGenericAccessModel model, User user) { return CanDo(model, user, ReadChar); }
        public bool CanUpdate(IGenericAccessModel model, User user) { return CanDo(model, user, UpdateChar); }
        public bool CanDelete(IGenericAccessModel model, User user) { return CanDo(model, user, DeleteChar); }

        public bool CheckAccessFormat(string access)
        {
            //Why do this manually? idk...
            Dictionary<char, int> counts = new Dictionary<char, int>();

            foreach(var character in access)
            {
                if(character != CreateChar && character != ReadChar && character != UpdateChar && character != DeleteChar)
                    return false;
                if(!counts.ContainsKey(character))
                    counts.Add(character, 0);
                if(++counts[character] > 1)
                    return false;
            }

            return true;
        }

        public bool CheckAccessFormat(GenericAccessView accessView)
        {
            return (CheckAccessFormat(accessView.baseAccess) && accessView.accessList.All(x => CheckAccessFormat(x.Value)));
        }
    }
}