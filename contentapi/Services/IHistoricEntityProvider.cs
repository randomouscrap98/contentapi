using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Models;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    //public interface IHistoricEntityProvider
    //{
    //    /// <summary>
    //    /// Regardless of ids in wrapper, write entirely new entity. All ids will be updated
    //    /// </summary>
    //    /// <param name="entity"></param>
    //    /// <returns></returns>
    //    Task WriteNewAsync(EntityWrapper entity);

    //    /// <summary>
    //    /// You'll want to call this every time you write either a new entity OR 
    //    /// update an entity. All IDs will be updated accordingly.
    //    /// </summary>
    //    /// <param name="entity"></param>
    //    /// <returns></returns>
    //    Task WriteWithHistoryAsync(EntityWrapper entity, long publicId = 0);

    //    /// <summary>
    //    /// Get the singular public ID for this entity.
    //    /// </summary>
    //    /// <param name="entity"></param>
    //    /// <returns></returns>
    //    Task<long> GetTrueId(EntityWrapper entity);

    //    /// <summary>
    //    /// Find an entity by PUBLIC facing ID.
    //    /// </summary>
    //    /// <param name="id"></param>
    //    /// <returns></returns>
    //    Task<EntityWrapper> FindByPublicIdAsync(long id);
    //}
}