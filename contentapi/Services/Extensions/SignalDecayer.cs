using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Randomous.EntitySystem;

namespace contentapi.Services.Extensions
{
    public class SignalDecayData<T>
    {
        //public TimeSpan DecayTime;
        public readonly object DecayLock = new object();
        public Dictionary<T, DateTime> DecayingList = new Dictionary<T, DateTime>();

        public void UpdateList(IEnumerable<T> items)
        {
            lock(DecayLock)
            {
                //First, boost the decays.
                foreach(var item in items)
                {
                    if(DecayingList.ContainsKey(item))
                        DecayingList[item] = DateTime.Now;
                    else
                        DecayingList.Add(item, DateTime.Now);
                }
            }
        }
        
        public List<T> ListDecay(TimeSpan decayTime)
        {
            lock(DecayLock)
            {
                //Then remove old items
                var removals = DecayingList.Where(x => DateTime.Now - x.Value > decayTime).Select(x => x.Key).ToList();
                removals.ForEach(x => DecayingList.Remove(x));

                return DecayingList.Select(x => x.Key).ToList();
            }
        }
    }

    //public static class SignalDecayer
    //{
    //    public static async Task<List<T>> ListenDecayAsync<T>(this ISignaler<List<T>> signaler, object id, Func<IQueryable<List<T>>, IQueryable<List<T>>> filter, TimeSpan maxWait, SignalDecayData<T> data)
    //    {
    //        var originalList = filter(new [] {data.ListDecay()}.AsQueryable()).ToList();

    //        if(originalList.Any())
    //            return originalList;

    //        return await signaler.ListenAsync(id, filter, maxWait);
    //    }

    //    public static Dictionary<List<T>, int> SignalDecayingItems<T>(this ISignaler<List<T>> signaler, List<T> items, SignalDecayData<T> data)
    //    {
    //        lock(data.DecayLock)
    //        {
    //            //First, boost the decays.
    //            foreach(var item in items)
    //            {
    //                if(data.DecayingList.ContainsKey(item))
    //                    data.DecayingList[item] = DateTime.Now;
    //                else
    //                    data.DecayingList.Add(item, DateTime.Now);
    //            }

    //            //Signal what's left
    //            return signaler.SignalItems(new [] { data.ListDecay()});
    //        }
    //    }
    //}
}