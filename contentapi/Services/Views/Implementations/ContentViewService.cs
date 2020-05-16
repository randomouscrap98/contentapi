using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class ContentViewService : BasePermissionViewService<ContentView, ContentSearch>
    {
        protected CategoryViewService categoryService;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        public ContentViewService(ViewServicePack services, ILogger<ContentViewService> logger, CategoryViewService categoryService, ContentViewSource converter) 
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
    }
}