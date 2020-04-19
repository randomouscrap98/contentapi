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

        /// <summary>
        /// Put a copy of the given entity (after modifications) into history
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected async Task<Entity> CopyToHistory(Entity entity)
        {
            var newEntity = new Entity(entity);
            newEntity.id = 0;
            newEntity.type = keys.HistoryKey + (newEntity.type ?? "");
            await provider.WriteAsync(newEntity);
            return newEntity;
        }

        /// <summary>
        /// Link all given values and relations to the given parent (do not write it!)
        /// </summary>
        /// <param name="values"></param>
        /// <param name="relations"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        protected void Relink(IEnumerable<EntityValue> values, IEnumerable<EntityRelation> relations, Entity parent)
        {
            foreach(var v in values)
                v.entityId = parent.id;
            foreach(var r in relations)
                r.entityId2 = parent.id;
        }

        /// <summary>
        /// Assuming a valid base entity, relink all items in the package by id
        /// </summary>
        /// <param name="package"></param>
        protected void Relink(EntityPackage package)
        {
            Relink(package.Values, package.Relations, package.Entity);
        }

        protected void FlattenPackage(EntityPackage package, List<EntityBase> collection)
        {
            collection.AddRange(package.Values);
            collection.AddRange(package.Relations);
            collection.Add(package.Entity);
        }

        protected List<EntityBase> FlattenPackage(EntityPackage package)
        {
            var result = new List<EntityBase>();
            FlattenPackage(package, result);
            return result;
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
            view .createUserId = view.editUserId;

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

        protected async Task<EntityPackage> WriteViewAsyncBase(V view)
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
                }
                else
                {
                    await provider.WriteAsync(package.Entity);
                    failDeletes.Add(package.Entity);

                    Relink(package);

                    writes.AddRange(package.Values);
                    writes.AddRange(package.Relations);
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
            return ConvertToView(await WriteViewAsyncBase(view));
        }

        ///// <summary>
        ///// Allow "fake" deletion of ANY historic entity (of any type)
        ///// </summary>
        ///// <param name="standinId"></param>
        ///// <returns></returns>
        //protected Task DeleteEntity(long standinId)
        //{
        //    return MarkLatestInactive(standinId, keys.DeleteAction);
        //}

        ///// <summary>
        ///// Check the entity for deletion. Throw exception if can't
        ///// </summary>
        ///// <param name="standinId"></param>
        ///// <returns></returns>
        //protected async virtual Task<EntityPackage> DeleteEntityCheck(long standinId)
        //{
        //    var last = await FindByIdAsync(standinId);

        //    if(last == null || !TypeIs(last.Entity.type, EntityType))
        //        throw new InvalidOperationException("No entity with that ID and type!");
        //    
        //    return last;
        //}

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


        protected async virtual Task<List<V>> ViewResult(IQueryable<Entity> query)
        {
            var packages = await provider.LinkAsync(query);
            return packages.Select(x => ConvertToView(x)).ToList();
        }
    }
}