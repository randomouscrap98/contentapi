using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
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

    public class CommentController : BaseSimpleController
    {
        public static IDecayer<CommentListener> listenDecayer = null;
        public static readonly object listenDecayLock = new object();

        public CommentController(ControllerServices services, ILogger<BaseSimpleController> logger, IDecayer<CommentListener> decayer) : base(services, logger)
        {
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
            var lastEdit = orderedRelations.LastOrDefault(x => x.type.StartsWith(keys.CommentHistoryHack));
            var last = orderedRelations.LastOrDefault();

            if(lastEdit != null)
            {
                view.editDate = (DateTime)lastEdit.createDateProper();
                view.editUserId = -lastEdit.entityId2;
            }

            view.deleted = last != null && last.type.StartsWith(keys.CommentDeleteHack);

            return view;
        }

        protected EntityRelation ConvertFromViewSimple(CommentView view)
        {
            var relation = services.mapper.Map<EntityRelation>(view);
            relation.type = keys.CommentHack;
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
            if (parent == null || !parent.Entity.type.StartsWith(keys.ContentType))
                throw new InvalidOperationException("Parent is not content!");

            return parent;
        }

        protected async Task<EntityPackage> ModifyCheckAsync(EntityRelation existing, long uid)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(existing.entityId1);

            //Only the owner can edit (until wee get permission overrides set up)
            if(existing.entityId2 != -uid)
                throw new UnauthorizedAccessException($"Cannot update comment {uid}");

            return parent;
        }

        protected async Task<EntityPackage> FullParentCheckAsync(long parentId, string action)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(parentId);

            //Create is full-on parent permission inheritance
            if (!CanCurrentUser(action, parent))
                throw new UnauthorizedAccessException($"Cannot perform this action in content {parent.Entity.id}");
            
            return parent;
        }

        protected async Task<EntityRelation> ExistingCheckAsync(long id)
        {
            //Have to go find existing.
            var existing = await provider.FindRelationByIdAsync(id);

            if (existing == null || !existing.type.StartsWith(keys.CommentHack) || existing.entityId2 == 0)
                throw new BadRequestException($"Couldn't find comment with id {id}");

            return existing;
        }

        protected EntityRelation MakeHistoryCopy(EntityRelation relation, string type)
        {
            var copy = new EntityRelation(relation);
            copy.id = 0;   //It's new though
            copy.entityId1 = -relation.id; //Point to the one we just gave (but make it negative because it's a relation to relation link)
            copy.entityId2 = -GetRequesterUid();
            copy.type = type + relation.entityId1.ToString();
            copy.createDate = DateTime.Now; //The history shows the edit date (confusingly, it's because this is the "update" record)

            return copy;
        }

        protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        {
            search = LimitSearch(search);
            search.TypeLike = $"{keys.CommentHack}%";
            return search;
        }

        [HttpGet]
        public async Task<ActionResult<List<CommentView>>> GetAsync([FromQuery]CommentSearch search)
        {
            var user = GetRequesterUidNoFail();
            logger.LogDebug($"Comment GetAsync called by {user}");

            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            //Entity1 is the content, content owns comments. The hack is entity2, which is not a child but a user.
            var query = BasicReadQuery(user, relationSearch, x => x.entityId1);

            var idHusk = ConvertToHusk(query, x => x.relation.id);
            var relations = await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch));

            return await ViewResult(relations);
        }

        [HttpGet("listen/{parentId}/listeners")]
        public Task<ActionResult<List<CommentListener>>> GetListenersAsync([FromRoute]long parentId, [FromQuery]List<long> lastListeners, CancellationToken token)
        {
            return ThrowToAction(async() =>
            {
                //Need to see if user has perms to read this.
                var parent = await FullParentCheckAsync(parentId, keys.ReadAction);

                DateTime start = DateTime.Now;
                var listenSet = lastListeners.ToHashSet();

                while (DateTime.Now - start < services.systemConfig.ListenTimeout)
                {
                    listenDecayer.UpdateList(GetListeners());
                    var result = listenDecayer.DecayList(services.systemConfig.ListenGracePeriod).Where(x => x.ContentListenId == parentId);

                    if (!result.Select(x => x.UserId).ToHashSet().SetEquals(listenSet))
                        return result.ToList();

                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                    token.ThrowIfCancellationRequested();
                }

                throw new TimeoutException("Ran out of time waiting for listeners");
            });
        }

        protected List<CommentListener> GetListeners(long parentId = -1)
        {
            var realListeners = provider.Listeners.Where(x => x.ListenerId is CommentListener).Select(x => (CommentListener)x.ListenerId);
            
            if(parentId > 0)
                realListeners = realListeners.Where(x => x.ContentListenId == parentId);
                
            return realListeners.ToList();
        }

        [HttpGet("listen/{parentId}")]
        [Authorize]
        public Task<ActionResult<List<CommentView>>> ListenAsync([FromRoute]long parentId, [FromQuery]long lastId, [FromQuery]long firstId, CancellationToken token)
        {
            return ThrowToAction(async () => 
            {
                var parent = await FullParentCheckAsync(parentId, keys.ReadAction);
                var listenId = new CommentListener() { UserId = GetRequesterUidNoFail(), ContentListenId = parentId };

                int entrances = 0;

                var comments = await services.provider.ListenAsync<EntityRelation>(listenId, 
                    (q) => 
                    {
                        entrances++;
                        var result = q.Where(x => 
                            //The new messages!
                            (x.entityId1 == parentId && (EF.Functions.Like(x.type, $"{keys.CommentHack}%") && x.id > lastId)) ||
                            //Edits to old ones (but only after the first pass!
                            ((x.type == $"{keys.CommentDeleteHack}{parentId}") || (x.type == $"{keys.CommentHistoryHack}{parentId}")) &&
                                (entrances > 1) && -x.entityId1 >= firstId);
                        
                        return result;
                    }, 
                    services.systemConfig.ListenTimeout, token);

                var goodComments = comments.Where(x => x.type.StartsWith(keys.CommentHack)).ToList(); //new List<EntityRelation>();
                var badComments = comments.Except(goodComments);

                if(badComments.Any())
                    goodComments.AddRange(await provider.GetEntityRelationsAsync(new EntityRelationSearch() { Ids = badComments.Select(x => -x.entityId1).ToList() }));

                return (await LinkAsync(goodComments)).Select(x => ConvertToView(x)).ToList();
            });
        }


        [HttpPost]
        [Authorize]
        public Task<ActionResult<CommentView>> PostAsync([FromBody]CommentView view)
        {
            return ThrowToAction<CommentView>(async () =>
            {
                view.id = 0;
                view.createDate = DateTime.Now;  //Ignore create date, it's always now
                view.createUserId = GetRequesterUid();    //Always requester

                var parent = await FullParentCheckAsync(view.parentId, keys.CreateAction);

                //now actually write the dang thing.
                var relation = ConvertFromViewSimple(view);
                await services.provider.WriteAsync(relation);
                return ConvertToViewSimple(relation);
            }); 
        }

        [HttpPut("{id}")]
        [Authorize]
        public Task<ActionResult<CommentView>> PutAsync([FromRoute]long id, [FromBody]CommentView view)
        {
            return ThrowToAction<CommentView>(async () =>
            {
                view.id = id;

                var uid = GetRequesterUidNoFail();
                var existing = await ExistingCheckAsync(id);

                view.createDate = (DateTime) existing.createDateProper();
                view.createUserId = -existing.entityId2; //creator should be original too

                var parent = await ModifyCheckAsync(existing, uid);

                var relation = ConvertFromViewSimple(view);

                //Write a copy of the current comment as historic
                var copy = MakeHistoryCopy(existing, keys.CommentHistoryHack);
                //relation.type = keys.CommentHackModified;

                await provider.WriteAsync(copy, relation);

                var package = new EntityRelationPackage() { Main = relation };
                package.Related.Add(copy);
                return ConvertToView(package);
            }); 
        }

        [HttpDelete("{id}")]
        [Authorize]
        public Task<ActionResult<CommentView>> DeleteAsync([FromRoute] long id)
        {
            return ThrowToAction<CommentView>(async () =>
            {
                var uid = GetRequesterUidNoFail();
                var existing = await ExistingCheckAsync(id);
                var parent = await ModifyCheckAsync(existing, uid);

                var copy = MakeHistoryCopy(existing, keys.CommentDeleteHack);
                existing.value = "";
                existing.entityId2 = 0;
                //existing.type = keys.CommentHackModified;
                await provider.WriteAsync(copy, existing);

                var relationPackage = (await LinkAsync(new [] {existing})).OnlySingle();
                return ConvertToView(relationPackage);
            }); 
        }
    }
}