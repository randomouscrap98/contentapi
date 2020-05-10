using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class CommentSearch : BaseParentSearch
    {
        public List<long> UserIds {get;set;}
    }

    public class CommentListener
    {
        public long UserId {get;set;}
        public long ContentListenId {get;set;}

        public override bool Equals(object obj)
        {
            if(obj != null && obj is CommentListener)
            {
                var listener = (CommentListener)obj;
                return listener.UserId == UserId && listener.ContentListenId == ContentListenId;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }

        public override string ToString()
        {
            return $"u{UserId}-c{ContentListenId}";
        }

    }

    public class CommentControllerProfile : Profile
    {
        public CommentControllerProfile()
        {
            CreateMap<CommentView, EntityRelation>()
                .ForMember(x => x.entityId1, o => o.MapFrom(s => s.parentId))
                .ForMember(x => x.entityId2, o => o.MapFrom(s => s.createUserId))
                .ForMember(x => x.value, o => o.MapFrom(s => s.content))
                .ReverseMap();

            CreateMap<CommentSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.ParentIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.UserIds.Select(x => -x).ToList()));
                //We actually CAN map parent ids directly
        }
    }

    public class EntityRelationPackage
    {
        public EntityRelation Main;
        public List<EntityRelation> Related = new List<EntityRelation>();
    }

    public class CommentViewService : BaseViewServices, IViewService<CommentView, CommentSearch>
    {
        public static IDecayer<CommentListener> listenDecayer = null;
        public static readonly object listenDecayLock = new object();

        protected SystemConfig config;

        public CommentViewService(ViewServicePack services, ILogger<CommentViewService> logger, IDecayer<CommentListener> decayer,
            SystemConfig config) : base(services, logger)
        {
            this.config = config; 

            lock(listenDecayLock)
            {
                //Use a SINGLE decayer
                if(listenDecayer == null)
                    listenDecayer = decayer;
            }
        }

        protected CommentView ConvertToViewSimple(EntityRelation relation)
        {
            var view = services.mapper.Map<CommentView>(relation);
            view.createUserId *= -1;

            //Mapper usually handles everything, but this is special
            view.createDate = (DateTime)relation.createDateProper();

            //Assume (bad assume!) that these are OK values
            view.editUserId = view.createUserId;
            view.editDate = view.createDate;

            return view;
        }

        protected CommentView ConvertToView(EntityRelationPackage package)
        {
            var view = ConvertToViewSimple(package.Main);
            var orderedRelations = package.Related.OrderBy(x => x.id);
            var lastEdit = orderedRelations.LastOrDefault(x => x.type.StartsWith(Keys.CommentHistoryHack));
            var last = orderedRelations.LastOrDefault();

            if(lastEdit != null)
            {
                view.editDate = (DateTime)lastEdit.createDateProper();
                view.editUserId = -lastEdit.entityId2;
            }

            view.deleted = last != null && last.type.StartsWith(Keys.CommentDeleteHack);

            return view;
        }

        protected EntityRelation ConvertFromViewSimple(CommentView view)
        {
            var relation = services.mapper.Map<EntityRelation>(view);
            relation.type = Keys.CommentHack;
            relation.entityId2 *= -1;
            return relation;
        }

        protected async Task<List<EntityRelationPackage>> LinkAsync(IEnumerable<EntityRelation> relations)
        {
            //This finds historical data (if there is any, it's probably none every time)
            var secondarySearch = new EntityRelationSearch();
            secondarySearch.EntityIds1 = relations.Select(x => -x.id).ToList();

            var historyRelations = await services.provider.GetEntityRelationsAsync(secondarySearch);

            return relations.Select(x => new EntityRelationPackage()
            {
                Main = x,
                Related = historyRelations.Where(y => y.entityId1 == -x.id).ToList()
            }).ToList();
        }

        protected async Task<List<CommentView>> ViewResult(IEnumerable<EntityRelation> relations)
        {
            return (await LinkAsync(relations)).Select(x => ConvertToView(x)).ToList();
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

        protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        {
            search = LimitSearch(search);
            search.TypeLike = $"{Keys.CommentHack}%";
            return search;
        }

        public async Task<IList<CommentView>> SearchAsync(CommentSearch search, Requester requester)
        {
            logger.LogTrace($"Comment GetAsync called by {requester}");

            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            //Entity1 is the content, content owns comments. The hack is entity2, which is not a child but a user.
            var query = BasicReadQuery(requester, relationSearch, x => x.entityId1);

            var relations = await services.provider.GetListAsync(FinalizeQuery<EntityRelation>(query, x=> x.relation.id, relationSearch));

            return await ViewResult(relations);
        }

        public async Task<List<CommentListener>> GetListenersAsync(long parentId, List<long> lastListeners, Requester requester, CancellationToken token)
        {
            //Need to see if user has perms to read this.
            var parent = await FullParentCheckAsync(parentId, Keys.ReadAction, requester);

            DateTime start = DateTime.Now;
            var listenSet = lastListeners.ToHashSet();

            while (DateTime.Now - start < config.ListenTimeout)
            {
                listenDecayer.UpdateList(GetListeners());
                var result = listenDecayer.DecayList(config.ListenGracePeriod).Where(x => x.ContentListenId == parentId);

                if (!result.Select(x => x.UserId).ToHashSet().SetEquals(listenSet))
                    return result.ToList();

                await Task.Delay(TimeSpan.FromSeconds(2), token);
                token.ThrowIfCancellationRequested();
            }

            throw new TimeoutException("Ran out of time waiting for listeners");
        }

        protected List<CommentListener> GetListeners(long parentId = -1)
        {
            var realListeners = provider.Listeners.Where(x => x.ListenerId is CommentListener).Select(x => (CommentListener)x.ListenerId);
            
            if(parentId > 0)
                realListeners = realListeners.Where(x => x.ContentListenId == parentId);
                
            return realListeners.ToList();
        }

        //This is a direct copy from the controller, eventually fix this to be more generic / better
        public async Task<List<CommentView>> ListenAsync(long parentId, long lastId, long firstId, Requester requester, CancellationToken token)
        {
            var parent = await FullParentCheckAsync(parentId, Keys.ReadAction, requester);
            var listenId = new CommentListener() { UserId = requester.userId, ContentListenId = parentId };

            int entrances = 0;

            var comments = await services.provider.ListenAsync<EntityRelation>(listenId,
                (q) =>
                {
                    entrances++;
                    var result = q.Where(x =>
                        //The new messages!
                        (x.entityId1 == parentId && (EF.Functions.Like(x.type, $"{Keys.CommentHack}%") && x.id > lastId)) ||
                        //Edits to old ones (but only after the first pass!
                        ((x.type == $"{Keys.CommentDeleteHack}{parentId}") || (x.type == $"{Keys.CommentHistoryHack}{parentId}")) &&
                            (entrances > 1) && -x.entityId1 >= firstId);

                    return result;
                },
                config.ListenTimeout, token);

            var goodComments = comments.Where(x => x.type.StartsWith(Keys.CommentHack)).ToList(); //new List<EntityRelation>();
            var badComments = comments.Except(goodComments);

            if (badComments.Any())
                goodComments.AddRange(await provider.GetEntityRelationsAsync(new EntityRelationSearch() { Ids = badComments.Select(x => -x.entityId1).ToList() }));

            return (await LinkAsync(goodComments)).Select(x => ConvertToView(x)).ToList();
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
            var relation = ConvertFromViewSimple(view);
            await services.provider.WriteAsync(relation);
            return ConvertToViewSimple(relation);
        }

        public async Task<CommentView> UpdateAsync(CommentView view, Requester requester)
        {
            var uid = requester.userId;
            var existing = await ExistingCheckAsync(view.id);

            view.createDate = (DateTime)existing.createDateProper();
            view.createUserId = -existing.entityId2; //creator should be original too

            var parent = await ModifyCheckAsync(existing, requester);

            var relation = ConvertFromViewSimple(view);

            //Write a copy of the current comment as historic
            var copy = MakeHistoryCopy(existing, Keys.CommentHistoryHack, uid);
            await provider.WriteAsync(copy, relation);

            var package = new EntityRelationPackage() { Main = relation };
            package.Related.Add(copy);
            return ConvertToView(package);

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

            var relationPackage = (await LinkAsync(new[] { existing })).OnlySingle();
            return ConvertToView(relationPackage);
        }

        public async Task<CommentView> FindByIdAsync(long id, Requester requester)
        {
            var search = new CommentSearch();
            search.Ids.Add(id);
            return (await SearchAsync(search, requester)).OnlySingle();
        }

        //Don't feel like implementing this right now.
        public Task<IList<CommentView>> GetRevisions(long id, Requester requester)
        {
            throw new NotImplementedException();
        }
    }
}