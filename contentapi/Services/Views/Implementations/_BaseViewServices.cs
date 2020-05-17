using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class ViewServicePack 
    {
        public IEntityProvider provider;
        public IMapper mapper;
        public IPermissionService permissions;
        public IHistoryService history;

        public ViewServicePack(IEntityProvider provider, IMapper mapper, IPermissionService permissions, IHistoryService history)
        {
            this.provider = provider;
            this.mapper = mapper;
            this.permissions = permissions;
            this.history = history;
        }
    }

    //The very most basic view service functions. Eventually, fix this to be services; don't have
    //time right now.
    public abstract class BaseViewServices<V,S> where S : IConstrainedSearcher
    {
        protected ViewServicePack services;
        protected ILogger logger;
        
        protected IEntityProvider provider => services.provider;

        public BaseViewServices(ViewServicePack services, ILogger<BaseViewServices<V,S>> logger)
        {
            this.services = services;
            this.logger = logger;
        }

        //protected EntityPackage NewEntity(string name, string content = null)
        //{
        //    return new EntityPackage()
        //    {
        //        Entity = new Entity() { 
        //            name = name, 
        //            content = content ,
        //            createDate = DateTime.UtcNow
        //        }
        //    };
        //}

        //protected EntityValue NewValue(string key, string value)
        //{
        //    return new EntityValue() 
        //    {
        //        key = key, 
        //        value = value, 
        //        createDate = null 
        //    };
        //}

        //protected EntityRelation NewRelation(long parent, string type, string value = null)
        //{
        //    return new EntityRelation()
        //    {
        //        entityId1 = parent,
        //        type = type,
        //        value = value,
        //        createDate = null
        //    };
        //}


        public Task<List<V>> SearchAsync(S search, Requester requester)
        {
            if(search.Limit < 0 || search.Limit > 1000)
                search.Limit = 1000;

            //Can now ALSO track views here perhaps...

            return PreparedSearchAsync(search, requester);
        }

        public abstract Task<List<V>> PreparedSearchAsync(S search, Requester requester);

        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<EntityValue> FindValueAsync(string type, string key, string value = null, long id = -1) //long.MinValue)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            
            var thing = 
                from v in provider.ApplyEntityValueSearch(provider.GetQueryable<EntityValue>(), valueSearch)
                join e in provider.GetQueryable<Entity>() on v.entityId equals e.id
                where EF.Functions.Like(e.type, $"{type}%")
                select v;

            return (await provider.GetListAsync(thing)).OnlySingle();
        }

        public IQueryable<E> Q<E>() where E : EntityBase
        {
            return provider.GetQueryable<E>();
        }

        public void FailUnlessSuper(Requester requester)
        {
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Must be super user to perform this action!");
        }
    }
}