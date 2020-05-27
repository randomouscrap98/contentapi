using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services.Implementations;
using contentapi.Views;

namespace contentapi.test
{
    public class ReadTestUnit
    {
        public UserView commonUser;
        public UserView specialUser;

        public ContentView commonContent;
        public ContentView specialContent;
    }

    public class ReadTestBase : UnitTestBase
    {
        protected ContentViewService contentService;
        protected UserViewService userService;
        protected ActivityViewSource activitySource;
        protected CommentViewService commentService;

        public ReadTestBase()
        {
            contentService = CreateService<ContentViewService>();
            userService = CreateService<UserViewService>();
            activitySource = CreateService<ActivityViewSource>();
            commentService = CreateService<CommentViewService>();
        }

        public async Task<ReadTestUnit> CreateUnitAsync()
        {
            //First, create some users!
            var unit = new ReadTestUnit();
            var requester = new Requester() { system = true };

            unit.commonUser = await userService.WriteAsync(new UserViewFull() { username = "commonUser" }, requester);
            unit.specialUser = await userService.WriteAsync(new UserViewFull() { username = "specialUser" }, requester);

            unit.commonContent = await contentService.WriteAsync(new ContentView() { name = "commonContent", parentId = 0, permissions = new Dictionary<string, string>() {{"0" , "cr" }} }, requester);
            unit.specialContent = await contentService.WriteAsync(new ContentView() { name = "specialContent", parentId = 0, permissions = new Dictionary<string, string>() {{unit.specialUser.id.ToString() , "cr" }} }, requester);

            return unit;
        }
    }
}