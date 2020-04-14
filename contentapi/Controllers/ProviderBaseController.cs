using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi
{
    public class ControllerServices//<T>
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public Keys keys;
        public SystemConfig systemConfig;

        public ControllerServices(/*ILogger<T> logger,*/ IEntityProvider provider, IMapper mapper, Keys keys, SystemConfig systemConfig)
        {
            this.provider = provider;
            //this.logger = logger;
            this.mapper = mapper;
            this.keys = keys;
            this.systemConfig = systemConfig;
        }
    }

    /// <summary>
    /// A bunch of methods extending the existing IProvider
    /// </summary>
    /// <remarks>
    /// Even though this extends from controller, it SHOULD NOT EVER use controller functions
    /// or fields or any of that. This is just a little silliness, I'm slapping stuff together.
    /// This is still testable without it being a controller though: please test sometime.
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public abstract class ProviderBaseController : ControllerBase
    {
        protected ControllerServices services;
        protected ILogger<ProviderBaseController> logger;
        
        protected Keys keys => services.keys;

        public ProviderBaseController(ControllerServices services, ILogger<ProviderBaseController> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        protected EntityPackage NewEntity(string name, string content = null)
        {
            return new EntityPackage()
            {
                Entity = new Entity() { name = name, content = content}
            };
        }

        protected EntityValue NewValue(string key, string value)
        {
            return new EntityValue() {key = key, value = value};
        }

        protected EntityRelation NewRelation(long parent, string type, string value = null)
        {
            return new EntityRelation()
            {
                entityId1 = parent,
                type = type,
                value = value
            };
        }

        /// <summary>
        /// Apply various limits to a search
        /// </summary>
        /// <param name="search"></param>
        /// <typeparam name="S"></typeparam>
        /// <returns></returns>
        protected virtual EntitySearch LimitSearch(EntitySearch search) //where S : EntitySearchBase
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;
            
            return search;
        }

        //protected long GetFaceId(EntityWrapper entity)
        //{
        //    if(!entity.HasValue(FaceKey))
        //        throw new InvalidOperationException("Entity has no historic link!");
        //    
        //    entity.id = long.Parse(entity.GetValue(FaceKey));
        //}

        //protected void SetAsHistory(Entity entity)
        //{
        //    entity.type = HistoryPrepend + entity.type;
        //}

        //protected Entity GetNewFaceEntity()
        //{
        //    var face = EntityWrapperExtensions.QuickEntity(FaceKey);
        //    face.type = FaceKey;
        //    //await entityProvider.WriteAsync(face);
        //    return face;
        //}

        ///// <summary>
        ///// Write a history-aware entity. Assumes the ID is actually the linkage base id and not 
        ///// the other thing.
        ///// </summary>
        ///// <param name="entity"></param>
        ///// <returns></returns>
        //protected async Task WriteHistoric(EntityWrapper entity) //, bool setFaceIdAsId = true)
        //{
        //    //var faceId = entity.id;
        //    var face = entity.Relations.FirstOrDefault(x => x.type == FaceKey);

        //    //if(faceId == 0)
        //    if(face == null)//!entity.HasRelation(FaceKey)) //!entity.HasValue(FaceKey))
        //    {   
        //        var faceEntity = GetNewFaceEntity();
        //        await entityProvider.WriteAsync(faceEntity);
        //        face = EntityWrapperExtensions.QuickRelation(faceEntity.id, entity.id, FaceKey);
        //        entity.Relations.Add(face); //AddRelation(faceEntity.id, FaceKey);
        //    }
        //        //faceId = (await WriteNewFaceAsync()).id;

        //    //Now set the entity as "new" and write it WITH the face relation.
        //    entity.SetEntityAsNew();
        //    //entity.AddRelation(faceId, FaceKey);
        //    await entityProvider.WriteAsync(entity);

        //    //Go back to the last entity with this face and set the type
        //    var otherEntities = await entityProvider.GetListAsync(entityProvider.GetQueryable<EntityRelation>().Where(x => x.type == FaceKey && x.entityId1 == face.id && x.entityId2 != entity.id));
        //    var updateEntities = await entityProvider.GetEntitiesAsync(new EntitySearch() {Ids = otherEntities.Select(x => x.entityId1).ToList()});
        //    updateEntities.ForEach(x => SetAsHistory(x));
        //    await entityProvider.WriteAsync(updateEntities.ToArray());

        //    //if(setFaceIdAsId)
        //    //    entity.id = faceId;
        //}

    }
}