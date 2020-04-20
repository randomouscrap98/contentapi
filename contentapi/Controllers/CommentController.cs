using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class CommentSearch : EntitySearchBase
    {
        public List<long> UserIds {get;set;}
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class ListenerId
    {
        public long UserId {get;set;}
        public long ContentListenId {get;set;}

        public override bool Equals(object obj)
        {
            if(obj != null && obj is ListenerId)
            {
                var listener = (ListenerId)obj;
                return listener.UserId == UserId && listener.ContentListenId == ContentListenId;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
    }

    public class CommentControllerProfile : Profile
    {
        public CommentControllerProfile()
        {
            CreateMap<CommentSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.ParentIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.UserIds.Select(x => -x).ToList()));
            CreateMap<CommentView, EntityRelation>()
                .ForMember(x => x.entityId1, o => o.MapFrom(s => s.parentId))
                .ForMember(x => x.entityId2, o => o.MapFrom(s => s.createUserId))
                .ForMember(x => x.value, o => o.MapFrom(s => s.content))
                .ReverseMap();
        }
    }

    public class EntityRelationPackage
    {
        public EntityRelation Main;
        public List<EntityRelation> Related = new List<EntityRelation>();
    }

    public class CommentController : BaseSimpleController
    {
        protected TimeSpan listenTime = TimeSpan.FromSeconds(300);
       // protected TimeSpan decayTime = TimeSpan.FromSeconds(5);
       // protected ISignaler<List<ListenerId>> signaler;
       // private static SignalDecayData<ListenerId> decayData = new SignalDecayData<ListenerId>();

        public CommentController(ControllerServices services, ILogger<BaseSimpleController> logger/*, ISignaler<ListenerId> signaler*/) : base(services, logger)
        {
            //this.signaler = signaler;
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

        protected IQueryable<EntityRRGroup> BasicPermissionQuery(IQueryable<EntityRelation> query, long user, string action)
        {
            var result = query.Join(services.provider.GetQueryable<EntityRelation>(), r => r.entityId1, r2 => r2.entityId2,
                      (r,r2) => new EntityRRGroup() { relation = r2, relation2 = r });

                //NOTE: the relations are SWAPPED because the intial group we applied the search to is the COMMENTS,
                //but permission where expects the FIRST relation to be permissions
            
            //This means you can only read comments if you can read the content. Meaning you may be unable to read your own comment.... oh well.
            result = PermissionWhere(result, user, action);
            return result;
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
            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            var user = GetRequesterUidNoFail();

            var query = BasicPermissionQuery(services.provider.ApplyEntityRelationSearch(services.provider.GetQueryable<EntityRelation>(), relationSearch, false), user, keys.ReadAction);

            var idHusk =
                from x in query 
                group x by x.relation2.id into g
                select new EntityBase() { id = g.Key };

            var relations = await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch));

            return await ViewResult(relations);
        }

        [HttpGet("listeners/{parentId}")]
        public ActionResult<List<ListenerId>> GetListenersAsync([FromRoute]long parentId)//, [FromQuery]List<long> lastListeners)
        {
            return GetListeners(parentId);
        }

        //[HttpGet("listeners/{parentId}")]
        //public async Task<ActionResult<List<ListenerId>>> GetListenersAsync([FromRoute]long parentId, [FromQuery]List<long> lastListeners)
        //{
        //    lastListeners = lastListeners ?? new List<long>();

        //    var listenSet = lastListeners.ToHashSet();

        //    var check = new Func<IQueryable<ListenerId>, IQueryable<ListenerId>>((q) =>
        //        q.Where(x => x.ContentListenId == parentId && listenSet.SequenceEqual(x.))
        //    );

        //    var originalList = check(decayData.GetList().AsQueryable()).ToList();

        //    if(originalList)

        //    return await signaler.ListenAsync(GetRequesterUidNoFail(), (q) => 
        //}

        public List<ListenerId> GetListeners(long parentId = -1)
        {
            var realListeners = provider.Listeners.Select(x => (ListenerId)x.ListenerId);
            
            if(parentId > 0)
                realListeners = realListeners.Where(x => x.ContentListenId == parentId);
                
            return realListeners.ToList();
        }

        [HttpGet("listen/{parentId}")]
        [Authorize]
        public Task<ActionResult<List<CommentView>>> ListenAsync([FromRoute]long parentId, [FromQuery]long lastId, [FromQuery]long firstId, [FromQuery]DateTime lastEdit)//[FromQuery]CommentSearch search)
        {
            return ThrowToAction(async () => 
            {
                var parent = await FullParentCheckAsync(parentId, keys.ReadAction);
                var listenId = new ListenerId() { UserId = GetRequesterUidNoFail(), ContentListenId = parentId };

                try
                {
                    //We know the list changes before and after listening. Signal both times, it'll be ok.
                    //signaler.SignalDecayingItems(GetListeners(), decayTime, decayData);

                    var comments = await services.provider.ListenAsync<EntityRelation>(listenId, 
                        (q) => q.Where(x => 
                            //The new messages!
                            (x.entityId1 == parentId && (EF.Functions.Like(x.type, $"{keys.CommentHack}%") && x.id > lastId)) ||
                            //Edits to old ones!
                            ((x.type == $"{keys.CommentDeleteHack}{parentId}") || (x.type == $"{keys.CommentHistoryHack}{parentId}"))
                              && x.createDate > lastEdit && -x.entityId1 >= firstId), 
                        listenTime);

                    //signaler.SignalDecayingItems(GetListeners(), decayTime, decayData);

                    var goodComments = comments.Where(x => x.type.StartsWith(keys.CommentHack)).ToList(); //new List<EntityRelation>();
                    var badComments = comments.Except(goodComments);

                    if(badComments.Any())
                        goodComments.AddRange(await provider.GetEntityRelationsAsync(new EntityRelationSearch() { Ids = badComments.Select(x => -x.entityId1).ToList() }));

                    return (await LinkAsync(goodComments)).Select(x => ConvertToView(x)).ToList();
                }
                catch (TimeoutException)
                {
                    return new List<CommentView>();
                }
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