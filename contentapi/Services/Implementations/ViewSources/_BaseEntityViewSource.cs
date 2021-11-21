using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class BaseEntityViewSourceServices : BaseViewSourceServices
    {
        public IHistoryService history {get;set;}

        public BaseEntityViewSourceServices(IMapper mapper, IEntityProvider provider, ICodeTimer timer,
            IHistoryService history) : base(mapper, provider, timer)
        {
            this.history = history;
        }
    }

    public abstract class BaseEntityViewSource<V,E,S> : BaseViewSource<V,EntityPackage,E,S>
        where V : BaseView where E : EntityGroup, new() where S : BaseHistorySearch, IConstrainedSearcher //where T : EntityPackage
    {
        public BaseEntityViewSource(ILogger<BaseViewSource<V,EntityPackage,E,S>> logger, BaseEntityViewSourceServices services)
            : base(logger, services) { }
        
        public abstract string EntityType {get;}
        public override Expression<Func<E, long>> MainIdSelector => x => x.entity.id;
        protected BaseEntityViewSourceServices entityServices => (BaseEntityViewSourceServices)services;

        public override async Task<List<EntityPackage>> RetrieveAsync(IQueryable<long> ids)
        {
            var ct = services.timer.StartTimer($"GetByIds:{GetType().Name}");
            var entities = await GetByIds<Entity>(ids);
            services.timer.EndTimer(ct);
            ct = services.timer.StartTimer($"LinkAsync:{GetType().Name}");
            var linked = await services.provider.LinkAsync(entities);
            ct.Name += $"({linked.Count})";
            services.timer.EndTimer(ct);
            return linked;//linked.Cast<EntityPackage>().ToList();
        }

        public async Task<List<V>> GetRevisions(long id)
        {
            var search = new EntitySearch();
            search.Ids = await entityServices.history.GetRevisionIdsAsync(id);
            var packages = await entityServices.provider.GetEntityPackagesAsync(search);
            return packages.OrderBy(x => x.Entity.id).Select(x => ToView(entityServices.history.ConvertHistoryToUpdate(x))).ToList();
        }

        
        public virtual EntitySearch CreateSearch(S search) //where S : BaseSearch
        {
            var entitySearch = services.mapper.Map<EntitySearch>(search);
            entitySearch.TypeLike = EntityType;
            return entitySearch;
        }

        public override async Task<IQueryable<E>> GetBaseQuery(S search)
        {
            var entitySearch = CreateSearch(search);

            return services.provider.ApplyEntitySearch(await Q<Entity>(), entitySearch, false)
                .Join(await Q<EntityRelation>(), e => e.id, r => r.entityId2, 
                (e, r) => new E() { entity = e, permission = r});
        }

        public override async Task<IQueryable<E>> ModifySearch(IQueryable<E> query, S search)
        {
            return await LimitByCreateEdit(await base.ModifySearch(query, search), search.CreateUserIds, search.EditUserIds);
        }

        public override async Task<IQueryable<long>> FinalizeQuery(IQueryable<E> query, S search)
        {
            if(search.Sort == "editdate")
            {
                var newGroups =  
                    from q in query
                    join r in await Q<EntityRelation>() on q.entity.id equals r.entityId2
                    where r.type == Keys.CreatorRelation
                    select new E{ entity = q.entity, relation = r };
                 
                var condense = newGroups.GroupBy(MainIdSelector).Select(x => new { key = x.Key, sort = x.Max(y => y.relation.createDate) });

                if(search.Reverse)
                    condense = condense.OrderByDescending(x => x.sort);
                else
                    condense = condense.OrderBy(x => x.sort);
                
                return condense.Select(x => x.key);
            }
            if(search.Sort == "name")
            {
                var condense = query.GroupBy(MainIdSelector).Select(x => new { key = x.Key, sort = x.Max(y => y.entity.name) });
                if(search.Reverse)
                    condense = condense.OrderByDescending(x => x.sort);
                else
                    condense = condense.OrderBy(x => x.sort);
                return condense.Select(x => x.key);
            }

            return await base.FinalizeQuery(query, search);
        }
    }
}