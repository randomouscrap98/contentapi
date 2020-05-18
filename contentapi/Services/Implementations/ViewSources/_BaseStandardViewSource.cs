using System;
using System.Linq;
using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseStandardViewSource<V,T,S> : BaseEntityViewSource<V,T,S>
        where V : StandardView where S : BaseParentSearch, IConstrainedSearcher where T : EntityPackage
    {
        protected BaseStandardViewSource(ILogger<BaseStandardViewSource<V,T,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public override IQueryable<EntityGroup> ModifySearch(IQueryable<EntityGroup> query, S search)
        {
            query = base.ModifySearch(query, search);

            if(search.ParentIds.Count > 0)
                query = LimitByParents(query, search.ParentIds);
            
            return query;
        }
    }
}