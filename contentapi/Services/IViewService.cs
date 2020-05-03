using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    //public class EntityGroup
    //{ 
    //    public Entity entity;
    //    public EntityRelation relation;
    //    public EntityValue value;
    //    public EntityRelation permission; 
    //}

    public class ViewRequester
    {
        public long userId;

        public override string ToString()
        {
            return $"{userId}";
        }
    }

    public interface IViewService<V,S> where S : EntitySearchBase where V : BaseView
    {
        Task<V> FindByIdAsync(long id, ViewRequester requester);
        Task<IList<V>> SearchAsync(S search, ViewRequester requester);
        Task<V> WriteAsync(V view, ViewRequester requester); //This can be either update or insert
        Task<V> DeleteAsync(long id, ViewRequester requester);

        Task<IList<V>> GetRevisions(long id, ViewRequester requester);
        
        //IQueryable<EntityGroup> GetBaseQueryable(S search);
    }
}