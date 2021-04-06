using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class CategorySearch : BaseContentSearch 
    { 
        public bool ComputeExtras {get;set;}
    }

    public class CategoryViewSource : BaseStandardViewSource<CategoryView, EntityPackage, EntityGroup, CategorySearch>
    {
        public override string EntityType => Keys.CategoryType;

        public CategoryViewSource(ILogger<CategoryViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) { }

        public override EntityPackage FromView(CategoryView view)
        {
            var package = NewEntity(view.name, view.description);
                ApplyFromStandard(view, package, Keys.CategoryType);

            foreach(var v in view.localSupers)
            {
                package.Add(new EntityRelation()
                {
                    entityId1 = v,
                    entityId2 = view.id,
                    createDate = null,
                    type = Keys.SuperRelation
                });
            }

            return package;
        }

        public List<long> BuildSupersForId(long id, Dictionary<long, List<long>> existing, IList<CategoryView> categories)
        {
            if(id <= 0) 
                return new List<long>();
            else if(existing.ContainsKey(id))
                return existing[id];
            
            var category = categories.FirstOrDefault(x => x.id == id);
        
            //This is a dangling category, the line ends here
            if(category == null)
                return new List<long>(); 
            
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

        public override CategoryView ToView(EntityPackage package)
        {
            var view = new CategoryView();
            this.ApplyToStandard(package, view);

            view.name = package.Entity.name;
            view.description = package.Entity.content;
            
            foreach(var v in package.Relations.Where(x => x.type == Keys.SuperRelation))
                view.localSupers.Add(v.entityId1);

            return view;
        }
    }
}