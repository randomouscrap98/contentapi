using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;

namespace contentapi.test.Overrides
{
    public class OpenController : EntityController<UserEntity, UserView>
    {
        public OpenController(EntityControllerServices services) : base(services) {}

        public long GetUid()
        {
            return services.session.GetCurrentUid();
        }
    }
}