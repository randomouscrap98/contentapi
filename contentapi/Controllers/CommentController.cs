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
    public class CommentController : BaseSimpleController
    {
        public CommentController(ControllerServices services, ILogger<BaseSimpleController> logger) : base(services, logger)
        {
        }

        protected CommentView ConvertToView(EntityRelation relation)
        {
            var view = services.mapper.Map<CommentView>(relation);
            view.createUserId *= -1;
            view.editDate = view.createDate; //Oh well, we'll fix this later
            return view;
        }

        protected EntityRelation ConvertFromView(CommentView view)
        {
            var relation = services.mapper.Map<EntityRelation>(view);
            relation.type = keys.CommentHack;
            relation.entityId2 *= -1;
            return relation;
        }


        [HttpGet]
        public async Task<ActionResult<List<CommentView>>> GetAsync([FromQuery]CommentSearch search)
        {
            var relationSearch = LimitSearch(services.mapper.Map<EntityRelationSearch>(search));
            relationSearch.TypeLike = keys.CommentHack;

            var user = GetRequesterUidNoFail();

            var query = services.provider.ApplyEntityRelationSearch(services.provider.GetQueryable<EntityRelation>(), relationSearch, false)
                .Join(services.provider.GetQueryable<EntityRelation>(), r => r.entityId1, r2 => r2.entityId2,
                      (r,r2) => new EntityRRGroup() { relation = r2, relation2 = r });

                //NOTE: the relations are SWAPPED because the intial group we applied the search to is the COMMENTS,
                //but permission where expects the FIRST relation to be permissions
            
            query = PermissionWhere(query, user, keys.ReadAction);

            var idHusk =
                from x in query 
                group x by x.relation2.id into g
                select new EntityBase() { id = g.Key };

            return (await services.provider.GetListAsync(FinalizeHusk<EntityRelation>(idHusk, relationSearch))).Select(x => ConvertToView(x)).ToList();
        }

        [HttpGet("listen/{parentId}")]
        public Task<ActionResult<List<CommentView>>> ListenAsync([FromRoute]long parentId, [FromQuery]long lastId)
        {
            return ThrowToAction(async () => 
            {
                await ParentCheckAync(parentId, keys.ReadAction);

                try
                {
                    var search = new EntityRelationSearch()
                    {
                        TypeLike = keys.CommentHack
                    };

                    search.EntityIds1.Add(parentId);
                    search = LimitSearch(search);

                    var comments = await services.provider.ListenNewAsync<EntityRelation>(lastId, TimeSpan.FromSeconds(300),
                        (q) => services.provider.ApplyEntityRelationSearch(q, search));

                    return comments.Select(x => ConvertToView(x)).ToList();
                }
                catch (TimeoutException)
                {
                    return new List<CommentView>();
                }
            });
        }

        protected async Task ParentCheckAync(long parentId, string permission = null)
        {
            var parent = await provider.FindByIdAsync(parentId);

            //Parent must be content
            if (parent == null || !parent.Entity.type.StartsWith(keys.ContentType))
                throw new InvalidOperationException("Parent is not content!");

            //Full inheritance from parent, that seems wrong.
            if(!string.IsNullOrEmpty(permission) && !CanCurrentUser(permission, parent))
                throw new UnauthorizedAccessException($"Cannot perform this action in content {parentId}");
        }

        protected Task<ActionResult<CommentView>> PostBase(CommentView view)
        {
            return ThrowToAction<CommentView>(async () =>
            {
                //Don't allow updates right now
                if(view.id > 0)
                    throw new InvalidOperationException("No comment editing right now!");
                
                view.createDate = DateTime.UtcNow;  //Ignore create date, it's always now
                view.createUserId = GetRequesterUid();    //Always requester

                //Go find the parent. If it's not content, BAD BAD BAD
                await ParentCheckAync(view.parentId, keys.CreateAction);

                //now actually write the dang thing.
                var relation = ConvertFromView(view);
                await services.provider.WriteAsync(relation);
                return ConvertToView(relation);
            }); 
        }

        [HttpPost]
        [Authorize]
        public Task<ActionResult<CommentView>> PostAsync([FromBody]CommentView view)
        {
            view.id = 0;
            return PostBase(view);
        }

        //[HttpPut("{id}")]
        //[Authorize]
        //public Task<ActionResult<CommentView>> PutAsync([FromRoute] long id, [FromBody]CommentView view)
        //{
        //    view.id = id;
        //    return PostBase(view);
        //}
    }
}