using System;
using System.Collections.Generic;

namespace contentapi.Services
{
    //public interface IDecayObject
    //{
    //    DateTime Expire {get;set;}
    //}

    public interface IDecayer<T> //where T : IDecayObject
    {
        void UpdateList(IEnumerable<T> items);
        List<T> DecayList(TimeSpan decay);
    }
}