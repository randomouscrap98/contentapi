using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Randomous.EntitySystem;

namespace contentapi.Services.Views
{
    public interface IViewSource<V,T,E,S> : IViewConverter<V, T> where E : EntityGroup
    {
        IQueryable<long> SearchIds(S search, Func<IQueryable<E>, IQueryable<E>> modify = null);
        Task<List<T>> RetrieveAsync(IQueryable<long> ids);
    }
}