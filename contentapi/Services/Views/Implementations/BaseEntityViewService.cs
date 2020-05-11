using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public abstract class BaseEntityViewService<V,S> : BaseViewServices, IViewService<V,S> where V : BaseEntityView where S : EntitySearchBase, new()
    {
        protected IViewConverter<V,EntityPackage> converter;

        public BaseEntityViewService(ViewServicePack services, ILogger<BaseEntityViewService<V,S>> logger, IViewConverter<V,EntityPackage> converter) 
            : base(services, logger) 
        { 
            this.converter = converter;
        }

        public abstract string EntityType {get;}

        public abstract Task<IList<V>> SearchAsync(S search, Requester requester);
        
        public async Task<V> FindByIdAsync(long id, Requester requester)
        {
            var search = new S();
            search.Ids.Add(id);
            return (await SearchAsync(search, requester)).OnlySingle();
        }

        /// <summary>
        /// Clean the view for general purpose, assume some defaults (run before udpate)
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        public virtual Task<V> CleanViewGeneralAsync(V view, Requester requester)
        {
            //These are safe, always true
            view.editUserId = requester.userId;       //Editor is ALWAYS US
            view.editDate = DateTime.Now;   //Edit date is ALWAYS NOW

            //These are assumptions, might get overruled
            view.createDate = view.editDate;
            view.createUserId = view.editUserId;

            return Task.FromResult(view);
        }

        /// <summary>
        /// Clean the view specifically for updates, run AFTER general
        /// </summary>
        /// <param name="view"></param>
        /// <param name="existing"></param>
        /// <returns></returns>
        public virtual Task<V> CleanViewUpdateAsync(V view, EntityPackage existing, Requester requester)
        {
            //FORCE these to be what they were before.
            view.createDate = (DateTime)existing.Entity.createDateProper();
            view.createUserId = existing.GetRelation(Keys.CreatorRelation).entityId1;

            //Don't allow posting over some other entity! THIS IS SUUUUPER IMPORTANT!!!
            if(!existing.Entity.type.StartsWith(EntityType))
                throw new BadRequestException($"No entity of proper type with id {view.id}");
            
            return Task.FromResult(view);
        }

        public virtual async Task<EntityPackage> WriteViewBaseAsync(V view, Requester requester, Action<EntityPackage> modifyBeforeCreate = null)
        {
            logger.LogTrace("WriteViewAsync called");

            EntityPackage existing = null; 

            view = await CleanViewGeneralAsync(view, requester);

            if(view.id != 0)
            {
                existing = await provider.FindByIdAsync(view.id);
                view = await CleanViewUpdateAsync(view, existing, requester);
            }

            //Now that the view they gave is all clean, do the full conversion! It should be safe!
            var package = converter.FromView(view);

            //If this is an UPDATE, do some STUFF
            if (view.id != 0)
                await services.history.UpdateWithHistoryAsync(package, requester.userId, existing);
            else
                await services.history.InsertWithHistoryAsync(package, requester.userId, modifyBeforeCreate);

            return package;
        }

        public virtual async Task<V> WriteAsync(V view, Requester requester)
        {
            return converter.ToView(await WriteViewBaseAsync(view, requester));
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public async Task<V> DeleteAsync(long entityId, Requester requester)
        {
            var package = await DeleteCheckAsync(entityId, requester);
            var view = converter.ToView(package);
            await services.history.DeleteWithHistoryAsync(package, requester.userId);
            return view;
        }

        public async Task<IList<V>> GetRevisions(long id, Requester requester)
        {
            var search = new EntitySearch();
            search.Ids = await services.history.GetRevisionIdsAsync(id);
            var packages = await provider.GetEntityPackagesAsync(search);
            return packages.Select(x => converter.ToView(services.history.ConvertHistoryToUpdate(x))).ToList();
        }

        /// <summary>
        /// Check the entity for deletion. Throw exception if can't
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public async virtual Task<EntityPackage> DeleteCheckAsync(long entityId, Requester requester)
        {
            var last = await provider.FindByIdAsync(entityId);

            if(last == null || !last.Entity.type.StartsWith(EntityType))
                throw new InvalidOperationException("No entity with that ID and type!");
            
            return last;
        }

        /// <summary>
        /// Modify a search converted from users so it works with real entities
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public EntitySearch ModifySearch(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);

            if(string.IsNullOrWhiteSpace(search.TypeLike))
                search.TypeLike = "%";

            search.TypeLike = EntityType + (search.TypeLike ?? "%");

            return search;
        }

        ///// <summary>
        ///// A shortcut for producing a list of views from a list of base entities
        ///// </summary>
        ///// <param name="query"></param>
        ///// <returns></returns>
        //public async virtual Task<List<V>> ViewResult(IQueryable<Entity> query)
        //{
        //    var packages = await provider.LinkAsync(query);
        //    return packages.Select(x => ConvertToView(x)).ToList();
        //}

        public async Task<T> FindByNameAsyncGeneric<T>(string name, Func<EntitySearch, Task<List<T>>> searcher)
        {
            return (await searcher(new EntitySearch() {NameLike = name, TypeLike = $"{EntityType}%"})).OnlySingle();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public Task<EntityPackage> FindByNameAsync(string name)
        {
            return FindByNameAsyncGeneric(name, provider.GetEntityPackagesAsync);
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public Task<Entity> FindByNameBaseAsync(string name)
        {
            return FindByNameAsyncGeneric(name, provider.GetEntitiesAsync);
        }

        public Task<EntityValue> FindValueAsync(string key, string value = null, long id = -1)
        {
            return FindValueAsync(EntityType, key, value, id);
        }

        public IQueryable<EntityGroup> BasicReadQuery(Requester requester, EntitySearch search)
        {
            return BasicReadQuery(requester, search, x => x.id);
        }

        public IQueryable<Entity> FinalizeQuery(IQueryable<EntityGroup> groups, EntitySearch search)
        {
            return FinalizeQuery<Entity>(groups, x => x.entity.id, search);
        }
    }
}