using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Models;
using Microsoft.EntityFrameworkCore;

namespace contentapi.Services
{
    public interface IEntityService
    {
        void SetNewEntity(EntityChild item, EntityAction defaultAction = EntityAction.None);
        T ConvertFromView<T,V>(V view) where T : EntityChild where V : EntityView;
        void FillExistingFromView<T,V>(V view, T existing) where T :EntityChild where V :EntityView;
        V ConvertFromEntity<T,V>(T entity) where T : EntityChild where V : EntityView;
        IQueryable<T> IncludeSet<T>(IQueryable<T> baseQuery) where T : EntityChild;
        Task IncludeSingleAsync<T>(T item, DbContext context) where T : EntityChild;
    }

    public class EntityService : IEntityService
    {
        protected IMapper mapper;
        protected AccessService accessService;

        public EntityService(IMapper mapper, AccessService accessService)
        {
            this.mapper = mapper;
            this.accessService = accessService;
        }

        public void SetNewEntity(EntityChild item, EntityAction defaultAction = EntityAction.None)
        {
            item.Entity = new Entity()
            {
                createDate = DateTime.Now,
                id = 0,
                status = 0,
                baseAllow = defaultAction,
                AccessList = new List<EntityAccess>() //EMPTY (hopefully efcore understands this)
            };
        }

        public T ConvertFromView<T,V>(V view) where T : EntityChild where V : EntityView
        {
            //First, fill the easy stuff by creating a new entity thing from the view's basic fields
            var result = mapper.Map<T>(view);

            //Fill up a new entity
            SetNewEntity(result);

            //Now convert the special stuff.
            accessService.FillEntityAccess(result.Entity, view);

            return result;
        }

        public void FillExistingFromView<T,V>(V view, T existing) where T :EntityChild where V :EntityView
        {
            mapper.Map(view, existing);
            //Interestingly, none of the "entity" fields are things users can change, SO we don't have to 
            //worry about that section!
            accessService.FillEntityAccess(existing.Entity, view);
        }

        public V ConvertFromEntity<T,V>(T entity) where T : EntityChild where V : EntityView
        {
            //First, fill easy stuff
            var result = mapper.Map<V>(entity);

            //Next, pull values from entity for view fields
            result.createDate = entity.Entity.createDate;
            result.id = entity.entityId;
            result.userId = entity.Entity.userId;

            //Then the complicated stuff.
            accessService.FillViewAccess(result, entity.Entity);

            return result;
        }

        public IQueryable<T> IncludeSet<T>(IQueryable<T> baseQuery) where T : EntityChild
        {
            return baseQuery.Include(x => x.Entity).ThenInclude(x => x.AccessList).AsQueryable();
        }

        public async Task IncludeSingleAsync<T>(T item, DbContext context) where T : EntityChild
        {
            await context.Entry(item).Reference(x => x.Entity).LoadAsync(); 
            await context.Entry(item.Entity).Collection(x => x.AccessList).LoadAsync();
        }
    }
}