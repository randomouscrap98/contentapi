using System;
using AutoMapper;
using contentapi.Models;

namespace contentapi.Services
{
    public interface IEntityService
    {
        void SetNewEntity(EntityChild item);
        T ConvertFromView<T,V>(V view) where T : EntityChild where V : EntityView;
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

        public void SetNewEntity(EntityChild item)
        {
            item.Entity = new Entity()
            {
                createDate = DateTime.Now,
                id = 0,
                status = 0,
                baseAllow = EntityAction.None,
                AccessList = new System.Collections.Generic.List<EntityAccess>() //EMPTY (hopefully efcore understands this)
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
    }
}