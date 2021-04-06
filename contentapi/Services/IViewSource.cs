using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    public interface IViewSource<V,T,E,S> : IViewConverter<V, T> where E : EntityGroup where S : IIdSearcher where V : IIdView
    {
        Task<IQueryable<long>> SearchIds(S search, Func<IQueryable<E>, IQueryable<E>> modify = null);
        Task<List<T>> RetrieveAsync(IQueryable<long> ids);
        Task<Dictionary<K, SimpleAggregateData>> GroupAsync<R,K>(IQueryable<long> ids, Expression<Func<R, K>> keySelector) where R : EntityBase;
    }
}