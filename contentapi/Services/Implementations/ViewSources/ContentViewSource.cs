using System;
using System.Linq;
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
    }
}