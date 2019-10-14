using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;

namespace contentapi.test.Overrides
{
    //TODO: MAKE THIS CONTENTCONTROLLER!
    public class OpenController : UsersController //AccessController<Content, ContentView>
    {
        public OpenController(GenericControllerServices services) : base(services) {} //, AccessService accessService) : base(services, accessService) { }

        public long GetUid()
        {
            return services.session.GetCurrentUid();
        }
    }
}