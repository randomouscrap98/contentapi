using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    public interface IHistoryService
    {
        Task<List<long>> GetRevisionIdsAsync(long packageId);

        Task UpdateWithHistoryAsync(EntityPackage updateData, long user, EntityPackage originalData = null);
        Task InsertWithHistoryAsync(EntityPackage newData, long user, Action<EntityPackage> modifyBeforeCreate = null);
        Task DeleteWithHistoryAsync(EntityPackage package, long user);

        EntityPackage ConvertHistoryToUpdate(EntityPackage history);
    }
}