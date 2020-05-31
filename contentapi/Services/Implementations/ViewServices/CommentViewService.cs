using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CommentViewService : BaseViewServices<CommentView, CommentSearch>, IViewRevisionService<CommentView, CommentSearch>
    {
        //protected SystemConfig config;
        protected CommentViewSource converter;
        protected WatchViewSource watchSource;

        public CommentViewService(ViewServicePack services, ILogger<CommentViewService> logger,
            /*SystemConfig config,*/ CommentViewSource converter, WatchViewSource watchSource) : base(services, logger)
        {
            //this.config = config; 
            this.converter = converter;
            this.watchSource = watchSource;
        }

        protected async Task<EntityPackage> BasicParentCheckAsync(long parentId)
        {
            var parent = await provider.FindByIdAsync(parentId);

            //Parent must be content
            if (parent == null || !parent.Entity.type.StartsWith(Keys.ContentType))
                throw new NotFoundException("Parent couldn't be found!");

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
                throw new NotFoundException("Comment or content not found"); //$"Cannot perform this action in content {parent.Entity.id}");
            
            return parent;
        }

        protected async Task<EntityRelation> ExistingCheckAsync(long id)
        {
            //Have to go find existing.
            var existing = await provider.FindRelationByIdAsync(id);

            if (existing == null || !existing.type.StartsWith(Keys.CommentHack) || existing.entityId2 == 0)
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

        public override async Task<List<CommentView>> PreparedSearchAsync(CommentSearch search, Requester requester)
        {
            logger.LogTrace($"Comment GetAsync called by {requester}");

            await FixWatchLimits(watchSource, requester, search.ContentLimit);

            return await converter.SimpleSearchAsync(search, q =>
                services.permissions.PermissionWhere(q, requester, Keys.ReadAction));
        }

        public class TempGroup
        {
            public long userId {get;set;}
            public long contentId {get;set;}
        }

        public async Task<List<CommentAggregateView>> SearchAggregateAsync(CommentSearch search, Requester requester)
        {
            //Repeat code, be careful
            await FixWatchLimits(watchSource, requester, search.ContentLimit);

            var ids = converter.SearchIds(search, q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));

            var groups = await converter.GroupAsync<EntityRelation,TempGroup>(ids, x => new TempGroup(){ userId = -x.entityId2, contentId = x.entityId1});

            return groups.ToLookup(x => x.Key.contentId).Select(x => new CommentAggregateView()
            {
                id = x.Key,
                count = x.Sum(y => y.Value.count),
                lastDate = x.Max(y => y.Value.lastDate),
                firstDate = x.Min(y => y.Value.firstDate),
                lastId = x.Max(y => y.Value.lastId),
                userIds = x.Select(y => y.Key.userId).Distinct().ToList()
            }).ToList();
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