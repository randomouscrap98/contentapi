using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Mapping;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
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

    public class ContentControllerProfile : Profile
    {
        public ContentControllerProfile()
        {
            CreateMap<ContentSearch, EntitySearch>()
                .ForMember(x => x.TypeLike, o => o.MapFrom(s => s.Type));
                //Can't do keyword, it's special search
        }
    }

    public class ContentViewService : BasePermissionViewService<ContentView, ContentSearch>
    {
        protected CategoryViewService categoryService;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        public ContentViewService(ViewServicePack services, ILogger<ContentViewService> logger, CategoryViewService categoryService, ContentMapper converter) 
            : base(services, logger, converter) 
        { 
            this.categoryService = categoryService;
        }

        public override string EntityType => Keys.ContentType;
        public override string ParentType => null;

        public List<long> BuildSupersForId(long id, Dictionary<long, List<long>> existing, IList<CategoryView> categories)
        {
            if(id <= 0) 
                return new List<long>();
            else if(existing.ContainsKey(id))
                return existing[id];
            
            var category = categories.FirstOrDefault(x => x.id == id);
        
            if(category == null)
                throw new InvalidOperationException($"Build super for non-existent id {id}");
            
            var ourSupers = new List<long>(category.localSupers);
            ourSupers.AddRange(BuildSupersForId(category.parentId, existing, categories));

            existing.Add(id, ourSupers.Distinct().ToList());

            return ourSupers;
        }

        public Dictionary<long, List<long>> GetAllSupers(IList<CategoryView> categories)
        {
            var currentCache = new Dictionary<long, List<long>>();

            foreach(var category in categories)
                BuildSupersForId(category.id, currentCache, categories);
            
            return currentCache;
        }

        public async Task SetupAsync()
        {
            var categories = await categoryService.SearchAsync(new CategorySearch(), new Requester() { system = true });
            cachedSupers = GetAllSupers(categories);
        }
        
        public override bool CanUser(Requester requester, string action, EntityPackage package)
        {
            var result = base.CanUser(requester, action, package);

            if(cachedSupers == null)
            {
                logger.LogWarning("CanUser called without cached supers");
            }
            else
            {
                var parentId = package.HasRelation(Keys.ParentRelation) ? package.GetRelation(Keys.ParentRelation).entityId1 : -1;
                result = result || action != Keys.ReadAction && 
                    (cachedSupers.ContainsKey(parentId) && cachedSupers[parentId].Contains(requester.userId) ||
                     cachedSupers.ContainsKey(package.Entity.id) && cachedSupers[package.Entity.id].Contains(requester.userId));
            }

            return result;
        }

        //public override EntityPackage CreateBasePackage(ContentView view)
        //{
        //    var package = NewEntity(view.name, view.content);

        //    //Need to add LOTS OF CRAP
        //    foreach(var keyword in view.keywords)
        //        package.Add(NewValue(Keys.KeywordKey, keyword));
        //    
        //    foreach(var v in view.values)
        //        package.Add(NewValue(Keys.AssociatedValueKey + v.Key, v.Value));
        //    
        //    //Bad coding, too many dependencies. We set the type without the base because someone else will do it for us.
        //    package.Entity.type = view.type;

        //    return package;
        //}

        //public override ContentView CreateBaseView(EntityPackage package)
        //{
        //    var view = new ContentView();
        //    view.name = package.Entity.name;
        //    view.content = package.Entity.content;
        //    view.type = package.Entity.type.Substring(EntityType.Length);

        //    foreach(var keyword in package.Values.Where(x => x.key == Keys.KeywordKey))
        //        view.keywords.Add(keyword.value);
        //    
        //    foreach(var v in package.Values.Where(x => x.key.StartsWith(Keys.AssociatedValueKey)))
        //        view.values.Add(v.key.Substring(Keys.AssociatedValueKey.Length), v.value);

        //    return view; //view;
        //}

        public override async Task<IList<ContentView>> SearchAsync(ContentSearch search, Requester requester)
        {
            logger.LogTrace($"Content SearchAsync called by {requester}");

            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));

            var initial = BasicReadQuery(requester, entitySearch);

            //Right now, entities are matched with very specific read relations and MAYBE some creator ones.
            //Be VERY CAREFUL, I get the feeling the entity count can get blown out of proportion with this massive join.

            if(search.ParentIds.Count > 0)
                initial = WhereParents(initial, search.ParentIds);

            if(!string.IsNullOrWhiteSpace(search.Keyword))
            {
                initial = initial
                    .Join(provider.GetQueryable<EntityValue>(), e => e.entity.id, v => v.entityId, 
                          (e,v) => new EntityGroup() { entity = e.entity, relation = e.relation, value = v})
                    .Where(x => x.value.key == Keys.KeywordKey && EF.Functions.Like(x.value.value, search.Keyword));
            }

            return await ViewResult(FinalizeQuery(initial, entitySearch), requester);
        }
    }
}