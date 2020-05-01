using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class BaseEntityController<V> : BaseSimpleController where V : BaseEntityView
    {
        public BaseEntityController(ControllerServices services, ILogger<BaseEntityController<V>> logger)
            :base(services, logger) { }

        protected abstract string EntityType {get;}

        /// <summary>
        /// Create a view with ONLY the unique fields for your controller filled in. You could fill in the
        /// others I guess, but they will be overwritten
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        protected abstract V CreateBaseView(EntityPackage package);

        /// <summary>
        /// Create a package with ONLY the unique fields for your controller filled in. 
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected abstract EntityPackage CreateBasePackage(V view);

        protected virtual V ConvertToView(EntityPackage package)
        {
            var view = CreateBaseView(package);

            var creatorRelation = package.GetRelation(keys.CreatorRelation);

            view.createDate = (DateTime)package.Entity.createDateProper();
            view.id = package.Entity.id;

            view.editDate = (DateTime)creatorRelation.createDateProper();
            view.createUserId = creatorRelation.entityId1;
            view.editUserId = long.Parse(creatorRelation.value);

            return view;
        }

        //TRUST the view. Assume it is written correctly, that createdate is set properly, etc.
        protected virtual EntityPackage ConvertFromView(V view)
        {
            var package = CreateBasePackage(view);

            package.Entity.id = view.id;
            package.Entity.type = EntityType + (package.Entity.type ?? "");
            package.Entity.createDate = view.createDate;

            var relation = NewRelation(view.createUserId, keys.CreatorRelation, view.editUserId.ToString());
            relation.createDate = view.editDate;
            package.Add(relation);

            return package;
        }



        /// <summary>
        /// Clean the view for general purpose, assume some defaults (run before udpate)
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected virtual Task<V> CleanViewGeneralAsync(V view)
        {
            //These are safe, always true
            view.editUserId = GetRequesterUidNoFail(); //Editor is ALWAYS US
            view.editDate = DateTime.Now;    //Edit date is ALWAYS NOW

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
        protected virtual Task<V> CleanViewUpdateAsync(V view, EntityPackage existing)
        {
            //FORCE these to be what they were before.
            view.createDate = (DateTime)existing.Entity.createDateProper();
            view.createUserId = existing.GetRelation(keys.CreatorRelation).entityId1;

            //Don't allow posting over some other entity! THIS IS SUUUUPER IMPORTANT!!!
            if(!existing.Entity.type.StartsWith(EntityType))
                throw new BadRequestException($"No entity of proper type with id {view.id}");
            
            return Task.FromResult(view);
        }

        protected async Task<EntityPackage> WriteViewBaseAsync(V view, Action<EntityPackage> modifyBeforeCreate = null)
        {
            logger.LogTrace("WriteViewAsync called");

            EntityPackage existing = null; 

            view = await CleanViewGeneralAsync(view);

            if(view.id != 0)
            {
                existing = await provider.FindByIdAsync(view.id);
                view = await CleanViewUpdateAsync(view, existing);
            }

            //Now that the view they gave is all clean, do the full conversion! It should be safe!
            var package = ConvertFromView(view);

            //If this is an UPDATE, do some STUFF
            if (view.id != 0)
            {
                await services.history.UpdateWithHistoryAsync(package, GetRequesterUidNoFail(), existing);
            }
            else
            {
                await services.history.InsertWithHistoryAsync(package, GetRequesterUidNoFail(), modifyBeforeCreate);
            }

            return package;
        }

        protected async Task<V> WriteViewAsync(V view)
        {
            return ConvertToView(await WriteViewBaseAsync(view));
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        protected async Task<V> DeleteByIdAsync(long entityId)
        {
            var package = await DeleteCheckAsync(entityId);
            var view = ConvertToView(package);
            await services.history.DeleteWithHistoryAsync(package, GetRequesterUidNoFail());
            return view;
        }

        /// <summary>
        /// Check the entity for deletion. Throw exception if can't
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        protected async virtual Task<EntityPackage> DeleteCheckAsync(long entityId)
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
        protected EntitySearch ModifySearch(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);

            if(string.IsNullOrWhiteSpace(search.TypeLike))
                search.TypeLike = "%";

            search.TypeLike = EntityType + (search.TypeLike ?? "%");

            return search;
        }

        /// <summary>
        /// A shortcut for producing a list of views from a list of base entities
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        protected async virtual Task<List<V>> ViewResult(IQueryable<Entity> query)
        {
            var packages = await provider.LinkAsync(query);
            return packages.Select(x => ConvertToView(x)).ToList();
        }

        protected async Task<T> FindByNameAsyncGeneric<T>(string name, Func<EntitySearch, Task<List<T>>> searcher)
        {
            return (await searcher(new EntitySearch() {NameLike = name, TypeLike = $"{EntityType}%"})).OnlySingle();
        }

        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        protected Task<EntityPackage> FindByNameAsync(string name)
        {
            return FindByNameAsyncGeneric(name, provider.GetEntityPackagesAsync);
        }


        /// <summary>
        /// Find some entity by name
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        protected Task<Entity> FindByNameBaseAsync(string name)
        {
            return FindByNameAsyncGeneric(name, provider.GetEntitiesAsync);
        }

        protected Task<EntityValue> FindValueAsync(string key, string value = null, long id = -1)
        {
            return FindValueAsync(EntityType, key, value, id);
        }

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntitySearch search)
        {
            return BasicReadQuery(user, search, x => x.id);
        }

        protected IQueryable<Entity> FinalizeQuery(IQueryable<EntityGroup> groups, EntitySearch search)
        {
            return FinalizeQuery<Entity>(groups, x => x.entity.id, search);
        }
    }
}