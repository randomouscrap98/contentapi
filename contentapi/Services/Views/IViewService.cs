using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views
{
    //public class EntityGroup
    //{ 
    //    public Entity entity;
    //    public EntityRelation relation;
    //    public EntityValue value;
    //    public EntityRelation permission; 
    //}

    //V and S don't have to be any particular kind of thing... there can be more exact derived interfaces
    //if you want, but sometimes a controller isn't specifically one or another thing.
    public interface IViewService<V,S>
    {
        Task<V> FindByIdAsync(long id, Requester requester);
        Task<IList<V>> SearchAsync(S search, Requester requester);
        Task<V> WriteAsync(V view, Requester requester); //This can be either update or insert
        Task<V> DeleteAsync(long id, Requester requester);

        Task<IList<V>> GetRevisions(long id, Requester requester);
        
        //IQueryable<EntityGroup> GetBaseQueryable(S search);
    }
}