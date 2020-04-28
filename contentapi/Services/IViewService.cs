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

    public interface IViewService<V,S> where S : EntitySearchBase where V : BaseView
    {
        Task<IList<V>> SearchAsync(S search);
        Task<V> WriteAsync(V view);
        
        //IQueryable<EntityGroup> GetBaseQueryable(S search);
    }
}