using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace contentapi.test.Overrides
{
    public class OpenController : EntityController<CategoryEntity, CategoryView>
    {
        public OpenController(EntityControllerServices services) : base(services) {}

        public long GetUid()
        {
            return services.session.GetCurrentUid();
        }

        public void ClearAllEntities()
        {
            services.context.Database.ExecuteSqlRaw("delete from categoryEntities");
        }

        public List<CategoryView> InsertRandom(int count, string baseAccess = "CRUD")
        {
            List<CategoryView> views = new List<CategoryView>();

            for(int i = 0; i < count; i++)
            {
                var result = Post(new CategoryView()
                {
                    name = $"insertRandom_{i}",
                    description = null,
                    type = "random",
                    parentId = null,
                    baseAccess = baseAccess
                }).Result;

                if(result.Value == null || result.Value.id <= 0)
                    throw new InvalidOperationException($"Could not insert random: {result.Result}");
                
                views.Add(result.Value);
            }

            return views;
        }
    }
}