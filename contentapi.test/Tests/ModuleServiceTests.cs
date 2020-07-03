using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Xunit;

namespace contentapi.test
{
    public class ModuleServiceTests : ServiceConfigTestBase<ModuleService, ModuleServiceConfig>
    {
        protected ModuleServiceConfig myConfig = new ModuleServiceConfig() { 
            ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
        };

        protected SqliteConnection masterconnection;

        protected override ModuleServiceConfig config => myConfig;

        public ModuleServiceTests()
        {
            masterconnection = new SqliteConnection(myConfig.ModuleDataConnectionString);
            masterconnection.Open();
        }

        ~ModuleServiceTests()
        {
            masterconnection.Close();
        }

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
    }
}