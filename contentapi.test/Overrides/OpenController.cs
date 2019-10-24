using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;
using Microsoft.EntityFrameworkCore;

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
    }
}