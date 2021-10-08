using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Xunit;

namespace contentapi.test
{
    public class ModuleServiceTests : ServiceConfigTestBase<ModuleService, ModuleServiceConfig>
    {
        protected override ModuleServiceConfig config => myConfig;
        protected ModuleMessageViewService moduleMessageService;
        protected UserViewService userService;

        protected ModuleServiceConfig myConfig = new ModuleServiceConfig() { 
            ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
        };

        protected SqliteConnection masterconnection;

        public ModuleServiceTests()
        {
            masterconnection = new SqliteConnection(myConfig.ModuleDataConnectionString);
            masterconnection.Open();

            moduleMessageService = CreateService<ModuleMessageViewService>();
            userService = CreateService<UserViewService>();
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
                function default(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "whatever", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void WrongSubcommands()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = ""wow""
                function default(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "whatever", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void EmptySubcommand()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={} }
                function command_wow(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow whatever", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void SubcommandFunction()
        {
            //The subcommands variable exists but has no argument list; we should still be able to redefine the function
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={[""function""]=""lolwut""} }
                function lolwut(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", " wow  whatever ", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void SubcommandArguments_Word()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={[""arguments""]={""first_word"",""second_word""}} }
                function command_wow(uid, word1, word2)
                    return ""Id: "" .. uid .. "" Word1: "" .. word1 .. "" Word2: "" .. word2
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow whatever stop", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Word1: whatever Word2: stop", result);
        }

        [Fact]
        public void EmptySubcommandKey()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""""]={[""arguments""]={""first_word"",""second_word""}} }
                function command_(uid, word1, word2)
                    return ""Id: "" .. uid .. "" Word1: "" .. word1 .. "" Word2: "" .. word2
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "whatever stop", new Requester() {userId = 99});
            Assert.Equal("Id: 99 Word1: whatever Word2: stop", result);
        }

        [Fact]
        public void SubcommandArguments_User()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={[""arguments""]={""first_user"",""second_user""}} }
                function command_wow(uid, user1, user2)
                    return ""Id: "" .. uid .. "" User1: "" .. user1 .. "" User2: "" .. user2
                end" 
            };
            //Fragile test, should inject a fake user service that always says the user is good. oh well
            userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
            userService.WriteAsync(new UserViewFull() { username = "dude2"}, new Requester() { system = true }).Wait();
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow 1 2(lol_username!)", new Requester() {userId = 8});
            Assert.Equal("Id: 8 User1: 1 User2: 2", result);
        }

        [Fact]
        public void SubcommandArguments_Mixed()
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={[""arguments""]={""first_user"",""second_word"",""third_freeform""}} }
                function command_wow(uid, user, word, freeform)
                    return ""Id: "" .. uid .. "" User: "" .. user .. "" Word: "" .. word .. "" Freeform: "" .. freeform
                end" 
            };
            userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow 1(somebody) kills a lot of people", new Requester() {userId = 8});
            Assert.Equal("Id: 8 User: 1 Word: kills Freeform: a lot of people", result);
        }

        [Theory]
        [InlineData(0, "Moments ago")]
        [InlineData(30.4, "30 seconds ago")]
        [InlineData(90, "1 minute ago")]
        [InlineData(601, "10 minutes ago")]
        [InlineData(7000, "1 hour ago")] //this is special, as it's close to 2 hours. We expect it (currently) to round down
        [InlineData(19000, "5 hours ago")] 
        [InlineData(3600 * 24 + 5, "1 day ago")] 
        [InlineData(3600 * 24 * 7 + 50, "7 days ago")] //Assume we don't have weeks
        [InlineData(3600 * 24 * 32 + 50, "1 month ago")] //Assume months are at least 30 days
        [InlineData(3600 * 24 * 31 * 11 + 50, "11 months ago")] 
        [InlineData(3600 * 24 * 365 * 8 + 50, "8 years ago")]  //Ah boy, years
        public void TimeSinceTimestamp(double subtractSeconds, string expected)
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, time)
                    return timesincetimestamp(time)
                end" 
            };
            userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", DateTime.Now.Subtract(TimeSpan.FromSeconds(subtractSeconds)).ToString(), new Requester() {userId = 8});
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("Some random string")]
        [InlineData("")]
        [InlineData(null)]
        public void SetGetData_Arbitrary(string data)
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, data)
                    setdata(""key"", data)
                    return getdata(""key"")
                end" 
            };
            userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", data, new Requester() {userId = 8});
            Assert.Equal(data, result);
        }

        [Theory]
        [InlineData("word", "abc", "string")]
        [InlineData("word", "123", "string")]
        [InlineData("int", "123", "number")]
        public void ArgTyping(string type, string data, string expected)
        {
            //The subcommands variable exists but is the wrong type, the module system shouldn't care
            var modview = new ModuleView() { name = "test", code = @"
                subcommands = {[""wow""]={[""arguments""]={""first_" + type + @"""}} }
                function command_wow(uid, data)
                    return type(data)
                end" 
            };
            userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow " + data, new Requester() {userId = 8});
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("First test")]
        [InlineData("Second test", "Another line")]
        [InlineData("", "And then!!", "OMG SO MUCH LOGGING")]
        public void PrntDbg(params string[] allmessages)
        {
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, data)
                    prntdbg(""Logging here!"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            foreach(var message in allmessages)
                service.RunCommand("test", message, new Requester() {userId = 8});

            Assert.Equal(allmessages.Length, mod.debug.Count);
            for(int i = 0; i < allmessages.Length; i++)
                Assert.Equal($"[8:default|{allmessages[i]}] Logging here!", mod.debug.ElementAt(i));
        }

        [Fact]
        public void BasicDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, data)
                    setdata(""myval"", ""something"")
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void SecondDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, data)
                    if data != nil then
                        setdata(""myval"", data)
                    end
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "something", new Requester() {userId = 8});
            Assert.Equal("something", result);
            result = service.RunCommand("test", null, new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void ReadMessagesInstant()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function default(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "whatever", requester);
            var messages = moduleMessageService.SearchAsync(new ModuleMessageViewSearch(), requester).Result; //service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
            Assert.Equal(requester.userId, messages.First().receiveUserId);
            Assert.Equal(requester.userId, messages.First().sendUserId);
        }

    }
}