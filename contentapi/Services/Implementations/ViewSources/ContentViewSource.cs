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

    public class ContentViewSource : BaseEntityViewSource, IViewSource<ContentView, EntityPackage, EntityGroup, ContentSearch>
    {
        public override string EntityType => Keys.ContentType;

        public ContentViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public EntityPackage FromView(ContentView view)
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

        public ContentView ToView(EntityPackage package)
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

        public override EntitySearch CreateSearch<S>(S search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += ((search as ContentSearch).Type ?? "%");
            return es;
        }

        public IQueryable<long> SearchIds(ContentSearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search);

            if(search.ParentIds.Count > 0)
                query = LimitByParents(query, search.ParentIds);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
                query = LimitByValue(query, Keys.KeywordKey, search.Keyword);

            if(modify != null)
                query = modify(query);

           //Special sorting routines go here

            return FinalizeQuery(query, search, x => x.entity.id);
        }

    }
}