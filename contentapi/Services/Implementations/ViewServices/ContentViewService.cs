using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ContentViewService : BasePermissionViewService<ContentView, ContentSearch>
    {
        protected CategoryViewSource categoryService;
        protected CommentViewSource commentSource;
        protected WatchViewSource watchSource;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        public ContentViewService(ViewServicePack services, ILogger<ContentViewService> logger, 
            CategoryViewSource categoryService, ContentViewSource converter,
            CommentViewSource commentSource, WatchViewSource watchSource) 
            : base(services, logger, converter) 
        { 
            this.categoryService = categoryService;
            this.commentSource = commentSource;
            this.watchSource = watchSource;
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
            var categories = await categoryService.SimpleSearchAsync(new CategorySearch());  //Just pull every dang category, whatever
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

        public override async Task<List<ContentView>> PreparedSearchAsync(ContentSearch search, Requester requester)
        {
            var baseResult = await base.PreparedSearchAsync(search, requester);

            //This requires intimate knowledge of how watches work. it's increasing the complexity/dependency,
            //but at least... I don't know, it's way more performant. Come up with some system perhaps after
            //you see what you need in other instances.
            var watches = await watchSource.GroupAsync<EntityRelation, long>(
                watchSource.SearchIds(new WatchSearch() { ContentIds = baseResult.Select(x => x.id).ToList() }),
                x => x.entityId2);

            var comments = await commentSource.GroupAsync<EntityRelation, long>(
                commentSource.SearchIds(new CommentSearch() { ParentIds = baseResult.Select(x => x.id).ToList() }),
                x => x.entityId1);
            
            baseResult.ForEach(x =>
            {
                if(watches.ContainsKey(-x.id))
                    x.watches = watches[-x.id];
                if(comments.ContainsKey(x.id))
                    x.comments = comments[x.id];
            });

            return baseResult;
        }
    }
}