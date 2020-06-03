using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.DependencyInjection;

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
        protected WatchViewService watchService;

        public ReadTestBase()
        {
            contentService = CreateService<ContentViewService>();
            userService = CreateService<UserViewService>();
            activitySource = CreateService<ActivityViewSource>();
            commentService = CreateService<CommentViewService>();
            watchService = CreateService<WatchViewService>();
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

    public class ReadTestBaseExtra : ReadTestBase
    {
        protected ReadTestUnit unit;
        protected CancellationTokenSource cancelSource;
        protected CancellationToken cancelToken;

        protected RelationListenerServiceConfig relationConfig = new RelationListenerServiceConfig();

        protected SystemConfig config = new SystemConfig()
        { 
            ListenTimeout = TimeSpan.FromSeconds(60),
            ListenGracePeriod = TimeSpan.FromSeconds(10)
        };

        public ReadTestBaseExtra() : base()
        {
            unit = CreateUnitAsync().Result;
            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;
        }

        public override IServiceCollection CreateServices()
        {
            var services = base.CreateServices();
            services.AddSingleton(config);
            services.AddSingleton(relationConfig);
            return services;
        }
    }
}