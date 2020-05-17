using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Views;

namespace contentapi.Services
{
    //V and S don't have to be any particular kind of thing... there can be more exact derived interfaces
    //if you want, but sometimes a controller isn't specifically one or another thing.
    public interface IViewReadService<V,S> where V : IIdView where S : IConstrainedSearcher
    {
        Task<List<V>> SearchAsync(S search, Requester requester);
    }

    public interface IViewService<V,S> : IViewReadService<V,S> where V : IIdView where S : IConstrainedSearcher 
    {
        Task<V> WriteAsync(V view, Requester requester); //This can be either update or insert
        Task<V> DeleteAsync(long id, Requester requester);
    }

    public interface IViewRevisionService<V,S> : IViewService<V,S> where V : IIdView where S : IConstrainedSearcher
    {
        Task<List<V>> GetRevisions(long id, Requester requester);
    }
}