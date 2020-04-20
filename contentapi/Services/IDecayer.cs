using System;
using System.Collections.Generic;

namespace contentapi.Services
{
    public interface IDecayer<T>
    {
        void UpdateList(IEnumerable<T> items);
        List<T> DecayList(TimeSpan decay);
    }
}