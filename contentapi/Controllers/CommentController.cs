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
        public CommentController(ControllerServices services, ILogger<BaseSimpleController> logger) : base(services, logger)
        {
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
            var lastEdit = package.Related.OrderBy(x => x.id).Last(x => x.type.StartsWith(keys.CommentHistoryHack));
            view.editDate = (DateTime)lastEdit.createDateProper();
            view.editUserId = -lastEdit.entityId2;
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
            secondarySearch.TypeLike = $"{keys.CommentHistoryHack}%";
            secondarySearch.EntityIds1 = relations.Select(x => x.id).ToList();

            var historyRelations = await services.provider.GetEntityRelationsAsync(secondarySearch);

            return relations.Select(x => new EntityRelationPackage()
            {
                Main = x,
                Related = historyRelations.Where(y => y.entityId1 == x.id).ToList()
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

        protected async Task<EntityPackage> FullParentCheckAsync(long parentId)
        {
            //Go find the parent. If it's not content, BAD BAD BAD
            var parent = await BasicParentCheckAsync(parentId);

            //Create is full-on parent permission inheritance
            if (!CanCurrentUser(keys.CreateAction, parent))
                throw new UnauthorizedAccessException($"Cannot perform this action in content {parent.Entity.id}");
            
            return parent;
        }

        protected async Task<EntityRelation> ExistingCheckAsync(long id)
        {
            //Have to go find existing.
            var existing = await provider.FindRelationByIdAsync(id);

            if (existing == null || existing.type != keys.CommentHack)
                throw new BadRequestException($"Couldn't find comment with id {id}");

            return existing;
        }

        protected EntityRelation MakeHistoryCopy(EntityRelation relation)
        {
            var copy = new EntityRelation(relation);
            copy.id = 0;   //It's new though
            copy.entityId1 = relation.id; //Point to the one we just gave
            copy.type = keys.CommentHistoryHack;

            return copy;
        }

        [HttpGet]
        public async Task<ActionResult<List<CommentView>>> GetAsync([FromQuery]CommentSearch search)
        {
            var relationSearch = LimitSearch(services.mapper.Map<EntityRelationSearch>(search));
            relationSearch.TypeLike = keys.CommentHack;

            var user = GetRequesterUidNoFail();

            var query = BasicPermissionQuery(services.provider.ApplyEntityRelationSearch(services.provider.GetQueryable<EntityRelation>(), relationSearch, false), user, keys.ReadAction);

            var idHusk =
                from x in query 
                group x by x.relation2.id into g
                select new EntityBase() { id = g.Key };

            var relations = await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch));

            return await ViewResult(relations);
        }

        [HttpGet("listen/{parentId}")]
        public Task<ActionResult<List<CommentView>>> ListenAsync([FromRoute]long parentId, [FromQuery]CommentSearch search)
        {
            return ThrowToAction(async () => 
            {
                if(!search.ParentIds.Contains(parentId))
                    search.ParentIds.Add(parentId);

                var parent = await BasicParentCheckAsync(parentId); //, keys.ReadAction);

                try
                {
                    var relationSearch = LimitSearch(services.mapper.Map<EntityRelationSearch>(search));
                    relationSearch.TypeLike = keys.CommentHack;

                    var comments = await services.provider.ListenAsync<EntityRelation>(lastId, 
                        (q) => services.provider.ApplyEntityRelationSearch(q, relationSearch),
                        TimeSpan.FromSeconds(300));

                    return comments.Select(x => ConvertToView(x)).ToList();
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
                view.createDate = DateTime.UtcNow;  //Ignore create date, it's always now
                view.createUserId = GetRequesterUid();    //Always requester

                var parent = await FullParentCheckAsync(view.parentId);

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
                view.createUserId = existing.entityId1; //creator should be original too

                var parent = await ModifyCheckAsync(existing, uid);

                var relation = ConvertFromViewSimple(view);

                //Write a copy of the current comment as historic
                var copy = MakeHistoryCopy(existing);
                await provider.WriteAsync(copy);
                //Keep the write as close to the trycatch as possible

                try
                {
                    //Now (hopefully) update the original.
                    await services.provider.WriteAsync(relation);
                }
                catch
                {
                    await provider.DeleteAsync(copy);
                    throw;
                }

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

                //Go link NOW, because later it's getting modified
                var relationPackage = (await LinkAsync(new [] {existing})).OnlySingle();

                existing.type = keys.CommentDeleteHack;
                await provider.WriteAsync(existing);

                return ConvertToView(relationPackage);
            }); 
        }
    }
}