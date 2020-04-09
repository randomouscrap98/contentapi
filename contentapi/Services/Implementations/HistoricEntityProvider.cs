using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //public class HistoricEntityProvider : IHistoricEntityProvider
    //{
    //    public IEntityProvider Provider { get; }

    //    protected ILogger<HistoricEntityProvider> logger;
    //    protected IMapper mapper;

    //    public HistoricEntityProvider(ILogger<HistoricEntityProvider> logger, IEntityProvider provider, IMapper mapper)
    //    {
    //        Provider = provider;
    //        this.logger = logger;
    //        this.mapper = mapper;
    //    }

    //    public async Task WriteNewAsync(EntityWrapper entity)
    //    {
    //        SetEntityAsNew(entity);
    //        await WriteAsync(entity);
    //    }

    //    public Task WriteWithHistoryAsync(EntityWrapper entity, long publicId)
    //    {
    //        //Delete last entity, write new entity. Or if there's no 
    //        throw new NotImplementedException();
    //    }

    //    public Task<long> GetTrueId(EntityWrapper entity)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<EntityWrapper> FindByPublicIdAsync(long id)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}