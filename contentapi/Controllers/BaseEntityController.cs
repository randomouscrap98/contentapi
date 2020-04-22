using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        protected void MakeHistoric(Entity entity)
        {
            entity.type = keys.HistoryKey + (entity.type ?? "");
        }

        /// <summary>
        /// Put a copy of the given entity (after modifications) into history
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected async Task<Entity> CopyToHistory(Entity entity)
        {
            var newEntity = new Entity(entity);
            newEntity.id = 0;
            MakeHistoric(newEntity);
            await provider.WriteAsync(newEntity);
            return newEntity;
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

            //Some are written, some are deleted on failure. All are eventually written.
            var writes = new List<EntityBase>();
            var failDeletes = new List<Entity>();

            try
            {
                //If this is an UPDATE, do some STUFF
                if (view.id != 0)
                {
                    var history = await CopyToHistory(existing.Entity);
                    failDeletes.Add(history);

                    //Bring all the existing over to this historic entity
                    Relink(existing.Values, existing.Relations, history);

                    //WE have to link the new stuff to US because we want to write everything all at once 
                    Relink(package.Values, package.Relations, existing.Entity);

                    var historyLink = NewRelation(existing.Entity.id, keys.HistoryRelation);
                    historyLink.entityId2 = history.id;
                    historyLink.createDate = DateTime.Now;

                    writes.Add(historyLink);
                    writes.AddRange(existing.Values);
                    writes.AddRange(existing.Relations);
                    FlattenPackage(package, writes);

                    writes.Add(MakeActivity(package, keys.UpdateAction, history.id.ToString()));
                }
                else
                {
                    await provider.WriteAsync(package.Entity);
                    failDeletes.Add(package.Entity);

                    Relink(package);
                    modifyBeforeCreate?.Invoke(package);

                    writes.AddRange(package.Values);
                    writes.AddRange(package.Relations);

                    writes.Add(MakeActivity(package, keys.CreateAction));
                }

                //Now try to write everything we added to the "transaction" (sometimes you just NEED an id and I can't let
                //efcore do it because I'm not using foreign keys)
                await provider.WriteAsync(writes.ToArray());
            }
            catch
            {
                await provider.DeleteAsync(failDeletes.ToArray());
                throw;
            }

            return package;
        }

        protected async Task<V> WriteViewAsync(V view)
        {
            return ConvertToView(await WriteViewBaseAsync(view));
        }

        /// <summary>
        /// Produce an activity for the given entity and action. Can include ONE piece of extra data.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="action"></param>
        /// <param name="extra"></param>
        /// <returns></returns>
        protected EntityRelation MakeActivity(EntityPackage package, string action, string extra = null, long userOverride = -1)
        {
            var activity = new EntityRelation();
            activity.entityId1 = userOverride >= 0 ? userOverride : GetRequesterUidNoFail(); //package.GetRelation(keys.CreatorRelation).entityId1; //GetRequesterUidNoFail();
            activity.entityId2 = -package.Entity.id; //It has to be NEGATIVE because we don't want them linked to content
            activity.createDate = DateTime.Now;
            activity.type = keys.ActivityKey + package.Entity.type;
            activity.value = action;

            if(!string.IsNullOrWhiteSpace(extra))
                activity.value += extra;

            return activity;
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
            MakeHistoric(package.Entity);
            await provider.WriteAsync<EntityBase>(package.Entity, MakeActivity(package, keys.DeleteAction, package.Entity.name));     //Notice it is a WRITe and not a delete.
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

        protected IQueryable<EntityGroup> BasicReadQuery(long user, EntitySearch search)
        {
            return BasicReadQuery(user, search, x => x.id);
        }

        protected IQueryable<EntityBase> ConvertToHusk(IQueryable<EntityGroup> groups)
        {
            return ConvertToHusk(groups, x => x.entity.id);
        }
    }
}