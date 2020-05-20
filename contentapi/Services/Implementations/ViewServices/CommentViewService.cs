using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CommentListener
    {
        public long UserId {get;set;}
        //public long ContentListenId {get;set;}
        public List<long> CommentListenParents {get;set;}

        public override bool Equals(object obj)
        {
            if(obj != null && obj is CommentListener)
            {
                var listener = (CommentListener)obj;
                return listener.UserId == UserId && listener.CommentListenParents.OrderBy(x => x).SequenceEqual(CommentListenParents.OrderBy(x => x)); //ContentListenId == ContentListenId;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public override string ToString()
        {
            return $"u{UserId}-c{string.Join(",", CommentListenParents)}";
        }

    }

    public class CommentViewService : BaseViewServices<CommentView, CommentSearch>, IViewRevisionService<CommentView, CommentSearch>
    {
        public static IDecayer<CommentListener> listenDecayer = null;
        public static readonly object listenDecayLock = new object();

        protected TimeSpan listenerPollingInterval = TimeSpan.FromSeconds(2);
        protected SystemConfig config;
        protected CommentViewSource converter;

        public CommentViewService(ViewServicePack services, ILogger<CommentViewService> logger, IDecayer<CommentListener> decayer,
            SystemConfig config, CommentViewSource converter) : base(services, logger)
        {
            this.config = config; 
            this.converter = converter;

            lock(listenDecayLock)
            {
                //Use a SINGLE decayer
                if(listenDecayer == null)
                    listenDecayer = decayer;
            }
        }

        protected async Task<EntityPackage> BasicParentCheckAsync(long parentId)
        {
            var parent = await provider.FindByIdAsync(parentId);

            //Parent must be content
            if (parent == null || !parent.Entity.type.StartsWith(Keys.ContentType))
                throw new InvalidOperationException("Parent is not content!");

            return parent;
        }

        protected async Task<EntityPackage> ModifyCheckAsync(EntityRelation existing, Requester requester)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(existing.entityId1);
            var uid = requester.userId;

            //Only the owner (and super users) can edit (until wee get permission overrides set up)
            if(existing.entityId2 != -uid && !services.permissions.IsSuper(requester))
                throw new UnauthorizedAccessException($"Cannot update comment {uid}");

            return parent;
        }

        protected async Task<EntityPackage> FullParentCheckAsync(long parentId, string action, Requester requester)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(parentId);

            //Create is full-on parent permission inheritance
            if (!services.permissions.CanUser(requester, action, parent))
                throw new UnauthorizedAccessException($"Cannot perform this action in content {parent.Entity.id}");
            
            return parent;
        }

        protected async Task<Dictionary<long, EntityPackage>> FullParentCheckAsync(List<long> parentIds, string action, Requester requester)
        {
            var result = new Dictionary<long, EntityPackage>();

            foreach(var id in parentIds)
                result.Add(id, await FullParentCheckAsync(id, action, requester));

            return result;
        }

        protected async Task<EntityRelation> ExistingCheckAsync(long id)
        {
            //Have to go find existing.
            var existing = await provider.FindRelationByIdAsync(id);

            if (existing == null || !existing.type.StartsWith(Keys.CommentHack) || existing.entityId2 == 0)
                throw new BadRequestException($"Couldn't find comment with id {id}");

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

        public override Task<List<CommentView>> PreparedSearchAsync(CommentSearch search, Requester requester)
        {
            logger.LogTrace($"Comment GetAsync called by {requester}");

            return converter.SimpleSearchAsync(search, q =>
                services.permissions.PermissionWhere(q, requester, Keys.ReadAction));
        }

        public class TempGroup
        {
            public long userId {get;set;}
            public long contentId {get;set;}
        }

        public async Task<List<CommentAggregateView>> SearchAggregateAsync(CommentSearch search, Requester requester)
        {
            var ids = converter.SearchIds(search, q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));

            var groups = await converter.GroupAsync<EntityRelation,TempGroup>(ids, x => new TempGroup(){ userId = -x.entityId2, contentId = x.entityId1});

            return groups.ToLookup(x => x.Key.contentId).Select(x => new CommentAggregateView()
            {
                id = x.Key,
                count = x.Sum(y => y.Value.count),
                lastPost = x.Max(y => y.Value.lastDate),
                firstPost = x.Min(y => y.Value.firstDate),
                userIds = x.Select(y => y.Key.userId).Distinct().ToList()
            }).ToList();
        }

        public async Task<Dictionary<long, List<CommentListener>>> GetListenersAsync(Dictionary<long, List<long>> lastListeners, Requester requester, CancellationToken token)
        {
            //Need to see if user has perms to read ANY of the parents
            var parents = await FullParentCheckAsync(lastListeners.Keys.ToList(), Keys.ReadAction, requester);

            DateTime start = DateTime.Now;
            var listenSet = lastListeners.ToDictionary(x => x.Key, y => y.Value.ToHashSet());

            //Creates a dictionary with pre-initialized keys. The keys won't change, we can keep redoing them.
            var result = lastListeners.ToDictionary(x => x.Key, y => new List<CommentListener>());

            while (DateTime.Now - start < config.ListenTimeout)
            {
                listenDecayer.UpdateList(GetListeners());

                //This list won't change as we're polling, so it's safe to keep writing over the old stuff.
                foreach(var parentKey in lastListeners)
                    result[parentKey.Key] = listenDecayer.DecayList(config.ListenGracePeriod).Where(x => x.CommentListenParents.Contains(parentKey.Key)).ToList();

                if (result.Any(x => !x.Value.Select(y => y.UserId).ToHashSet().SetEquals(listenSet[x.Key])))
                    return result;

                await Task.Delay(listenerPollingInterval, token);
                token.ThrowIfCancellationRequested();
            }

            throw new TimeoutException("Ran out of time waiting for listeners");
        }

        protected List<CommentListener> GetListeners(long parentId = -1)
        {
            var realListeners = provider.Listeners.Where(x => x.ListenerId is CommentListener).Select(x => (CommentListener)x.ListenerId);
            
            if(parentId > 0)
                realListeners = realListeners.Where(x => x.CommentListenParents.Contains(parentId));
                
            return realListeners.ToList();
        }

        public async Task<List<CommentView>> ListenAsync(List<long> parentIds, long lastId, long firstId, Requester requester, CancellationToken token)
        {
            //Ensure we can read all the parents they're asking for. We will also show up in every room you're listening to.
            var parents = await FullParentCheckAsync(parentIds, Keys.ReadAction, requester);
            var listenId = new CommentListener() { UserId = requester.userId, CommentListenParents = parentIds };

            var stringParents = parentIds.Select(x => x.ToString());

            int entrances = 0;

            var comments = await services.provider.ListenAsync<EntityRelation>(listenId, (q) =>
            {
                entrances++;

                var result = q.Where(x =>
                    //The new messages!
                    (parentIds.Contains(x.entityId1) && (EF.Functions.Like(x.type, $"{Keys.CommentHack}%") && x.id > lastId)) ||
                    //Edits to old ones (will be filtered out special later, EFCore can't do too much, which is first pass)
                    (EF.Functions.Like(x.type, $"{Keys.CommentDeleteHack}%") || EF.Functions.Like(x.type, $"{Keys.CommentHistoryHack}%")) && -x.entityId1 >= firstId);
                
                if(entrances <= 1)
                {
                    //Ignore anything other than new comments on the first pass. The query is too complex
                    //to do in efcore
                    result = result.Where(x => EF.Functions.Like(x.type, $"{Keys.CommentHack}%"));
                }
                else
                {
                    //This can be a more complex query, since it's not using efcore. This is awful programming, but it's
                    //how EntitySystem listening is designed and I'm not going to redesign it all right now. This works...
                    result = result.Where(x => EF.Functions.Like(x.type, $"{Keys.CommentHack}%") || 
                        stringParents.Contains(x.type.Substring(Keys.CommentHistoryHack.Length)));
                }

                return result;
            },
            config.ListenTimeout, token);

            //"Good" comments are ones that can be used "as-is". Bad comments are ones that need to be modified.
            var goodComments = comments.Where(x => x.type.StartsWith(Keys.CommentHack)).ToList(); //new List<EntityRelation>();
            var badComments = comments.Except(goodComments);

            if (badComments.Any())
                goodComments.AddRange(await provider.GetEntityRelationsAsync(new EntityRelationSearch() { Ids = badComments.Select(x => -x.entityId1).ToList() }));

            return (await converter.LinkAsync(goodComments)).Select(x => converter.ToView(x)).ToList();
        }

        public Task<CommentView> WriteAsync(CommentView view, Requester requester)
        {
            if(view.id == 0)
                return InsertAsync(view, requester);
            else
                return UpdateAsync(view, requester);
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

            var relation = converter.FromViewSimple(view);

            //Write a copy of the current comment as historic
            var copy = MakeHistoryCopy(existing, Keys.CommentHistoryHack, uid);
            await provider.WriteAsync(copy, relation);

            var package = new EntityRelationPackage() { Main = relation };
            package.Related.Add(copy);
            return converter.ToView(package);

        }

        public async Task<CommentView> DeleteAsync(long id, Requester requester)
        {
            var uid = requester.userId;
            var existing = await ExistingCheckAsync(id);
            var parent = await ModifyCheckAsync(existing, requester);

            var copy = MakeHistoryCopy(existing, Keys.CommentDeleteHack, uid);
            existing.value = "";
            existing.entityId2 = 0;
            await provider.WriteAsync(copy, existing);

            var relationPackage = (await converter.LinkAsync(new[] { existing })).OnlySingle();
            return converter.ToView(relationPackage);
        }

        //Don't feel like implementing this right now.
        public Task<List<CommentView>> GetRevisions(long id, Requester requester)
        {
            throw new NotImplementedException();
        }
    }
}