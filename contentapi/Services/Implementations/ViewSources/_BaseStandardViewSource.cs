using System;
using System.Linq;
using System.Threading.Tasks;
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
        protected BaseStandardViewSource(ILogger<BaseStandardViewSource<V,T,E,S>> logger, BaseViewSourceServices services)
            : base(logger, services) {}
        
        public override async Task<IQueryable<E>> ModifySearch(IQueryable<E> query, S search)
        {
            query = await base.ModifySearch(query, search);

            if(search.ParentIds.Count > 0)
            {
                var limited = await LimitByParents(query, search.ParentIds);

                if(search.ParentIds.Contains(0))
                {
                    if(search.ParentIds.Count == 1)
                        query = await GetOrphans(query);
                    else
                        query = limited.Union(await GetOrphans(query));
                }
                else
                {
                    query = limited;
                }
            }
            
            if(!string.IsNullOrEmpty(search.AssociatedKey) || !string.IsNullOrEmpty(search.AssociatedValue))
                query = await LimitByValue(query, (Keys.AssociatedValueKey + search.AssociatedKey ?? "%"), search.AssociatedValue ?? "%");
            
            return query;
        }
    }
}