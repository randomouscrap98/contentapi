using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CommentRethread
    {
        public List<long> commentIds {get;set;} = new List<long>();
        public long newParent {get;set;}
    }

    public class CommentViewService : BaseViewServices<CommentView, CommentSearch>, IViewRevisionService<CommentView, CommentSearch>
    {
        protected CommentViewSource converter;
        protected WatchViewSource watchSource;
        protected BanViewSource banSource;
        protected ContentViewSource contentSource;
        protected ICodeTimer timer;
        protected CacheService<long, CommentView> singlecache;

        public CommentViewService(ViewServicePack services, ILogger<CommentViewService> logger,
            CommentViewSource converter, WatchViewSource watchSource, BanViewSource banSource, 
            ContentViewSource contentSource, ICodeTimer timer, CacheService<long, CommentView> singlecache) : base(services, logger)
        {
            this.converter = converter;
            this.watchSource = watchSource;
            this.timer = timer;
            this.banSource = banSource;
            this.contentSource = contentSource;
            this.singlecache = singlecache;
        }

        protected virtual async Task<EntityPackage> BasicParentCheckAsync(long parentId, Requester requester)
        {
            var parent = await provider.FindByIdAsync(parentId);

            //Parent must be content
            if (parent == null || !parent.Entity.type.StartsWith(Keys.ContentType))
                throw new BadRequestException("Parent couldn't be found!");

            //Banning is such a "base" thing
            var ban = await banSource.GetUserBan(requester.userId);

            //This just means they can't create public content, but they can still make private stuff... idk
            if(ban != null && (ban.type == BanType.@public && services.permissions.CanUser(new Requester() { userId = 0}, Keys.ReadAction, parent)))
                throw new BannedException(ban.message);

            return parent;
        }

        protected async Task<EntityPackage> ModifyCheckAsync(EntityRelation existing, Requester requester)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(existing.entityId1, requester);
            var uid = requester.userId;

            //Only the owner (and super users) can edit (until wee get permission overrides set up)
            if(existing.entityId2 != -uid && !services.permissions.IsSuper(requester))
                throw new ForbiddenException($"Cannot update comment {existing.id}");

            return parent;
        }

        protected async Task<EntityPackage> FullParentCheckAsync(long parentId, string action, Requester requester)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(parentId, requester);

            //Create is full-on parent permission inheritance
            if (!services.permissions.CanUser(requester, action, parent))
                throw new ForbiddenException($"Cannot perform action on parent {parentId}"); //$"Cannot perform this action in content {parent.Entity.id}");
            
            return parent;
        }

        protected async Task<EntityRelation> ExistingCheckAsync(long id)
        {
            //Have to go find existing.
            var existing = await provider.FindRelationByIdAsync(id);

            if (existing == null || !existing.type.StartsWith(converter.EntityType) || existing.entityId2 == 0)
                throw new NotFoundException($"Couldn't find comment with id {id}");

            return existing;
        }

        protected EntityRelation MakeHistoryCopy(EntityRelation relation, string type, long userId)
        {
            var copy = new EntityRelation(relation);
            copy.id = 0;   //It's new though
            copy.entityId1 = -relation.id; //Point to the one we just gave (but make it negative because it's a relation to relation link)
            copy.entityId2 = -userId;
            copy.type = type + relation.entityId1.ToString();
            copy.createDate = DateTime.Now; //The history shows the edit date (confusingly, it's because this is the "update" record)

            return copy;
        }

        protected async Task OptimizedCommentSearch(CommentSearch search, Requester requester, Func<Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>>, Task> perform)
        {
            //If fixing the watch limits automatically safe limited us, we don't have to perform the (sometimes lengthy) permission calculation again.
            var safeLimited = await FixWatchLimits(watchSource, requester, search.ContentLimit);

            converter.JoinPermissions = false;

            try
            {
                var limitIds = search.ContentLimit.Limit.Select(x => x.id).ToList();

                //An optimization: regardless of what the USER asked for, a content limit query will ONLY return
                //rows (comments) where the comment parent is within the limiter. This enables the later optimization
                if(limitIds.Count > 0)
                {
                    search.ParentIds = limitIds;
                }

                //We can do some special stuff to increase performance if we STILL have no parentids
                if(search.ParentIds.Count == 0)
                {
                    var tempSearch = services.mapper.Map<CommentSearch>(search);
                    tempSearch.Limit = -1; //Remove limits, we're grouping.

                    if(search.Ids.Count > 0)
                    {
                        //This is a more optimized group because it does not require a join
                        search.ParentIds = (await converter.GroupAsync<EntityRelation, long>(
                            await converter.GetBaseQuery(tempSearch), g=>g.relation, converter.PermIdSelector)).Select(x => x.Key).ToList();
                    }
                    else if(search.CreateEnd.Ticks > 0 || search.CreateStart.Ticks > 0 || search.MinId > 0 || search.MaxId > 0)
                    {
                        search.ParentIds = (await converter.GroupAsync(await converter.SearchIds(tempSearch), converter.PermIdSelector)).Select(x => x.Key).ToList();
                    }
                }

                if(search.ParentIds.Count > 0)
                {
                    //If we're already safe limited, don't reperform the parent id limit
                    if(!safeLimited)
                    {
                        //Limit parentids by the ones this requester is allowed to have.
                        var ids = await contentSource.SearchIds(new ContentSearch() { Ids = search.ParentIds }, q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));
                        search.ParentIds = await services.provider.GetListAsync(ids);
                        search.ParentIds.Add(long.MaxValue); //Ensures it stays at "empty" parentids
                    }

                    await perform(null);
                }
                else
                {
                    converter.JoinPermissions = true;
                    await perform(q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));
                }
            }
            finally
            {
                converter.JoinPermissions = true;
            }
        }

        public override async Task<List<CommentView>> PreparedSearchAsync(CommentSearch search, Requester requester)
        {
            logger.LogTrace($"Comment GetAsync called by {requester}");

            List<CommentView> result = null;

            //Write a regression test for comment deletion alerts 
            if(search.Ids.Count > 0 && OnlyIdSearch(search, requester))
            {
                result = singlecache.GetValues(search.Ids);

                //NOTE: there is a period of time where the cache could be invalid by the time you get this data. I'm willing
                //to accept slightly out of date information... but does that mean some people will NEVER get the updates? no,
                //it just means for THIS request, they may have something slightly amiss.
                if (result.Select(x => x.id).OrderBy(x => x).SequenceEqual(search.Ids.Distinct().OrderBy(x => x)))
                {
                    //THIS IS A WIN! It means we have all the comments we were searching for! Now just remove the ones you're NOT ALLOWED to see
                    var allowedIds = await services.provider.GetListAsync(await contentSource.SearchIds(
                        new ContentSearch() { Ids = result.Select(x => x.parentId).ToList() },
                        q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction))
                    );

                    //Remove any that don't show up in allowed ids
                    result.RemoveAll(x => !allowedIds.Contains(x.parentId));
                    logger.LogDebug($"* Using in-memory comments ({string.Join(",", result.Select(x => x.id))}) for ids {string.Join(",", search.Ids)}");

                    return result;
                }
            }

            await OptimizedCommentSearch(search, requester, async (f) => result = await converter.SimpleSearchAsync(search, f));
            return result;
        }

        public class TempGroup
        {
            public long userId {get;set;}
            public long contentId {get;set;}
        }

        //public async Task<List<CommentAggregateView>> SearchAggregateAsync(CommentSearch search, Requester requester)
        public Task<List<CommentAggregateView>> SearchAggregateAsync(CommentSearch search, Requester requester)
        {
            return Task.FromResult(new List<CommentAggregateView>());

            //IQueryable<long> ids = null;
            //await OptimizedCommentSearch(search, requester, async (f) => ids = await converter.SearchIds(search, f));

            //var groups = await converter.GroupAsync<EntityRelation,TempGroup>(ids, x => new TempGroup(){ userId = -x.entityId2, contentId = x.entityId1});

            //return groups.ToLookup(x => x.Key.contentId).Select(x => new CommentAggregateView()
            //{
            //    id = x.Key,
            //    count = x.Sum(y => y.Value.count),
            //    lastDate = x.Max(y => y.Value.lastDate),
            //    firstDate = x.Min(y => y.Value.firstDate),
            //    lastId = x.Max(y => y.Value.lastId),
            //    userIds = x.Select(y => y.Key.userId).Distinct().ToList()
            //}).ToList();
        }

        public async Task<CommentView> WriteAsync(CommentView view, Requester requester)
        {
            var t = timer.StartTimer($"Write cmt p{view.parentId}:u{view.createUserId}");

            try
            {
                CommentView result = null;

                if (view.id == 0)
                {
                    result = await InsertAsync(view, requester);
                }
                else
                {
                    //Need to get rid of the cache BEFORE the update
                    singlecache.FlushKeys(new[] { view.id });
                    result = await UpdateAsync(view, requester);
                }

                singlecache.StoreItem(result.id, result, true);

                return result;
            }
            finally
            {
                timer.EndTimer(t);
            }
        }

        public async Task<CommentView> InsertAsync(CommentView view, Requester requester)
        {
            view.id = 0;
            view.createDate = DateTime.Now;  //Ignore create date, it's always now
            view.createUserId = requester.userId;    //Always requester

            var parent = await FullParentCheckAsync(view.parentId, Keys.CreateAction, requester);

            //now actually write the dang thing.
            var relation = converter.FromViewSimple(view);
            await services.provider.WriteAsync(relation);
            return converter.ToViewSimple(relation);
        }

        public async Task<CommentView> UpdateAsync(CommentView view, Requester requester)
        {
            var uid = requester.userId;
            var existing = await ExistingCheckAsync(view.id);

            view.createDate = (DateTime)existing.createDateProper();
            view.createUserId = -existing.entityId2; //creator should be original too

            var parent = await ModifyCheckAsync(existing, requester);

            var originalUpdate = converter.FromViewSimple(view);
            var relation = new EntityRelation(existing);

            //We can ONLY update the content!
            relation.value = originalUpdate.value;

            //Write a copy of the current comment as historic
            var copy = MakeHistoryCopy(existing, Keys.CommentHistoryHack, uid);
            await provider.WriteAsync(copy, relation);

            var package = new EntityRelationPackage() { Main = relation };
            package.Related.Add(copy);
            return converter.ToView(package);
        }

        public async Task<CommentView> DeleteAsync(long id, Requester requester)
        {
            //Even if it doesn't succeed, get rid of it asap
            singlecache.FlushKeys(new[] { id });

            var uid = requester.userId;
            var existing = await ExistingCheckAsync(id);
            var parent = await ModifyCheckAsync(existing, requester);

            var copy = MakeHistoryCopy(existing, Keys.CommentDeleteHack, uid);
            existing.value = "";
            existing.entityId2 = 0;
            await provider.WriteAsync(copy, existing);

            var relationPackage = (await converter.LinkAsync(new List<EntityRelation>() { existing })).OnlySingle();
            return converter.ToView(relationPackage);
        }

        //Don't feel like implementing this right now.
        public Task<List<CommentView>> GetRevisions(long id, Requester requester)
        {
            throw new NotImplementedException();
        }
    }
}