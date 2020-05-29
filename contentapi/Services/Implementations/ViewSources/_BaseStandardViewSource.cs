using System;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public abstract class BaseStandardViewSource<V,T,E,S> : BaseEntityViewSource<V,T,E,S>
        where V : StandardView where E : EntityGroup, new() where S : BaseContentSearch, IConstrainedSearcher where T : EntityPackage
    {
        protected BaseStandardViewSource(ILogger<BaseStandardViewSource<V,T,E,S>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }
        
        public override IQueryable<E> ModifySearch(IQueryable<E> query, S search)
        {
            query = base.ModifySearch(query, search);

            if(search.ParentIds.Count > 0)
            {
                var limited = LimitByParents(query, search.ParentIds);

                if(search.ParentIds.Contains(0))
                {
                    if(search.ParentIds.Count == 1)
                        query = GetOrphans(query);
                    else
                        query = limited.Union(GetOrphans(query));
                }
                else
                {
                    query = limited;
                }
            }
            
            if(!string.IsNullOrEmpty(search.AssociatedKey) || !string.IsNullOrEmpty(search.AssociatedValue))
                query = LimitByValue(query, (Keys.AssociatedValueKey + search.AssociatedKey ?? "%"), search.AssociatedValue ?? "%");
            
            return query;
        }
    }
}