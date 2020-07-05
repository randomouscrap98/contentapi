using System;
using System.Threading;
using contentapi.Configs;
using contentapi.Services;
using contentapi.Services.Implementations;
//using contentapi.test.Implementations;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace contentapi.test
{
    [Collection("ASYNC")]
    public class ModuleViewServiceTests : ServiceConfigTestBase<ModuleViewService, SystemConfig>
    {
        protected SystemConfig sysConfig = new SystemConfig();
        protected UserViewService userService;
        protected UserViewFull superUser;
        protected UserViewFull basicUser;
        protected Requester system = new Requester() { system = true };
        protected Requester super;
        protected Requester basic;

        protected override SystemConfig config => sysConfig;


        protected ModuleServiceConfig myConfig = new ModuleServiceConfig() { 
            ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
        };

        protected SqliteConnection masterconnection;


        public override IServiceCollection CreateServices()
        {
            var result = base.CreateServices();
            result.AddSingleton(myConfig);
            //result.AddSingleton<IModuleService, FakeModuleService>();
            return result;
        }

        public ModuleViewServiceTests()
        {
            userService = CreateService<UserViewService>();
            superUser = userService.WriteAsync(new UserViewFull() { username = "mysuper" }, system).Result;
            basicUser = userService.WriteAsync(new UserViewFull() { username = "basic" }, system).Result;
            super = new Requester() { userId = superUser.id };
            basic = new Requester() { userId = basicUser.id };
            sysConfig.SuperUsers.Add(superUser.id);

            masterconnection = new SqliteConnection(myConfig.ModuleDataConnectionString);
            masterconnection.Open();
        }

        ~ModuleViewServiceTests()
        {
            masterconnection.Close();
        }

        [Fact]
        public void TestSuperWrite()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
        }
    
        [Fact]
        public void TestBasicNonWrite()
        {
            AssertThrows<AuthorizationException>(() => service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, basic).Wait());
        }

        [Fact]
        public void TestSuperDelete()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
            service.DeleteAsync(module.id, super ).Wait();
        }

        [Fact]
        public void TestNonSuperDelete()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
            AssertThrows<AuthorizationException>(() => service.DeleteAsync(module.id, basic).Wait());
        }

        [Fact]
        public void TestUpdate()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.code == "--wow");
            module.code = "--grnadfket";
            var module2 = service.WriteAsync(module, super).Result; //This should NOT throw an exception, using the same id so update
            Assert.True(module.code == "--grnadfket");
        }

        [Fact]
        public void TestUniqueName()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.name == "test");
            module.id = 0; //Make it a "new" module
            AssertThrows<BadRequestException>(() => service.WriteAsync(module, super).Wait()); //Oops, can't write the same name
        }

        [Fact]
        public void TestDoubleInsert()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.name == "test");
            var module2 = service.WriteAsync(new ModuleView() { name = "test2", code = "--wow"}, super).Result;
            Assert.True(module2.name == "test2");

            var results = service.SearchAsync(new ModuleSearch(), super).Result;
            Assert.Equal(2, results.Count);
        }

        //protected override ModuleServiceConfig config => myConfig;


        [Fact]
        public void BasicCreate()
        {
            var modview = new ModuleView() { name = "test", code = "--wow"};
            var mod = service.UpdateModule(modview);
            Assert.True(mod.script != null);
        }

        [Fact]
        public void BasicParameterPass()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void BasicDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    setdata(""myval"", ""something"")
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void SecondDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    setdata(""myval"", ""something"")
                    return getdata(""myval"")
                end
                function command_wow2(uid, data)
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
            result = service.RunCommand("test", "wow2", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void ReadMessagesInstant()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
            Assert.Equal(requester.userId, messages.First().receiverUid);
            Assert.Equal(requester.userId, messages.First().senderUid);
        }

        [Fact]
        public void ReadMessagesListen()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
            var lastId = messages.Last().id;
            var messageWait = service.ListenAsync(lastId, requester, TimeSpan.FromSeconds(1), CancellationToken.None);
            AssertNotWait(messageWait);
            result = service.RunCommand("test", "wow", "whatever", requester);
            messages = AssertWait(messageWait);
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
            Assert.True(messages.First().id > lastId);
        }

        [Fact]
        public void ReadMessagesListen0()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var messageWait = service.ListenAsync(0, requester, TimeSpan.FromSeconds(1), CancellationToken.None);
            AssertNotWait(messageWait);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = AssertWait(messageWait);
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
        }
    }
}