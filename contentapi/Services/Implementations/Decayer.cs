using System;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Implementations
{
    public class Decayer<T> : IDecayer<T>
    {
        protected readonly object decayLock = new object();
        protected Dictionary<T, DateTime> decayingList = new Dictionary<T, DateTime>();

        public void UpdateList(IEnumerable<T> items)
        {
            lock(decayLock)
            {
                //First, boost the decays.
                foreach(var item in items)
                {
                    //FULLY refresh the item by REMOVING IT (just in case!)
                    if(decayingList.ContainsKey(item))
                        decayingList.Remove(item);

                    decayingList.Add(item, DateTime.Now);
                }
            }
        }
        
        public List<T> DecayList(TimeSpan decayTime)
        {
            lock(decayLock)
            {
                //Then remove old items
                var removals = decayingList.Where(x => DateTime.Now - x.Value > decayTime).Select(x => x.Key).ToList();
                removals.ForEach(x => decayingList.Remove(x));

                return decayingList.Select(x => x.Key).ToList();
            }
        }
    }
}