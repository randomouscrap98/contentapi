using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ContentSearch : BaseContentSearch
    {
        public string Keyword {get;set;}
        public string Type {get;set;}
    }

    public class ContentViewSourceProfile : Profile
    {
        public ContentViewSourceProfile()
        {
            //Can't do keyword, it's special search
            CreateMap<ContentSearch, EntitySearch>()
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.Type));
        }
    }

    public class ContentViewSource : BaseStandardViewSource<ContentView, EntityPackage, EntityGroup, ContentSearch>
    {
        public override string EntityType => Keys.ContentType;

        public ContentViewSource(ILogger<ContentViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override EntityPackage FromView(ContentView view)
        {
            var package = this.NewEntity(view.name, view.content);
            this.ApplyFromStandard(view, package, Keys.ContentType);

            foreach(var v in view.keywords)
            {
                package.Add(new EntityValue()
                {
                    entityId = view.id,
                    key = Keys.KeywordKey,
                    value = v,
                    createDate = null
                });
            }
            
            package.Entity.type += view.type;

            return package;
        }

        public override ContentView ToView(EntityPackage package)
        {
            var view = new ContentView();
            this.ApplyToStandard(package, view);

            view.name = package.Entity.name;
            view.content = package.Entity.content;
            view.type = package.Entity.type.Substring(Keys.ContentType.Length);

            foreach(var keyword in package.Values.Where(x => x.key == Keys.KeywordKey))
                view.keywords.Add(keyword.value);
            
            //votes are special: they can only be read
            view.votes.@public = package.Relations.Where(x => x.type.StartsWith(Keys.VoteRelation))
                .ToDictionary(x => x.entityId1.ToString(), y => new VoteData() { vote = y.type == Keys.UpvoteRelation ? 1 : -1, date = y.createDateProper() });

            view.votes.up = view.votes.@public.Count(x => x.Value.vote > 0);
            view.votes.down = view.votes.@public.Count(x => x.Value.vote < 0);

            return view;
        }

        public override EntitySearch CreateSearch(ContentSearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += (search.Type ?? "%");
            return es;
        }

        public override IQueryable<EntityGroup> ModifySearch(IQueryable<EntityGroup> query, ContentSearch search)
        {
            query = base.ModifySearch(query, search);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
                query = LimitByValue(query, Keys.KeywordKey, search.Keyword);
            
            return query;
        }


        public Task<List<EntityRelation>> GetUserVotes(long userId, long contentId)
        {
            var oldSearch = new EntityRelationSearch();
            oldSearch.EntityIds1.Add(userId);
            oldSearch.EntityIds2.Add(contentId);
            oldSearch.TypeLike = Keys.VoteRelation + "%";

            return provider.GetEntityRelationsAsync(oldSearch);
        }

        public EntityRelation CreateBaseVote(long userId, long contentId)
        {
            return new EntityRelation()
            {
                entityId1 = userId,
                entityId2 = contentId,
                createDate = DateTime.UtcNow,
            };
        }

        public EntityRelation CreateDownVote(long userId, long contentId)
        {
            var vote = CreateBaseVote(userId, contentId);
            vote.type = Keys.DownvoteRelation;
            return vote;
        }

        public EntityRelation CreateUpVote(long userId, long contentId)
        {
            var vote = CreateBaseVote(userId, contentId);
            vote.type = Keys.UpvoteRelation;
            return vote;
        }
    }
}