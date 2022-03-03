using System;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Module;
using contentapi.Search;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class ModuleServiceTests : UnitTestBase //ServiceConfigTestBase<ModuleService, ModuleServiceConfig>
{
    protected ModuleServiceConfig config;
    protected IGenericSearch searcher;
    protected ModuleService service;
    //protected ModuleMessageViewService moduleMessageService;
    //protected UserViewService userService;

    //protected ModuleServiceConfig myConfig = new ModuleServiceConfig() { 
    //    ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
    //};

    protected SqliteConnection masterconnection;

    public ModuleServiceTests()
    {
        config = new ModuleServiceConfig() {
            ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
        };

        searcher = GetService<IGenericSearch>();
        service = new ModuleService(config, GetService<ILogger<ModuleService>>(), GetService<ModuleMessageAdder>(), searcher);
        masterconnection = new SqliteConnection(config.ModuleDataConnectionString);
        masterconnection.Open();

        //moduleMessageService = CreateService<ModuleMessageViewService>();
        //userService = CreateService<UserViewService>();
    }

    ~ModuleServiceTests()
    {
        masterconnection.Close();
    }

    [Fact]
    public void BasicCreate()
    {
        var modview = new ContentView() { name = "test", text = "--wow"};
        var mod = service.UpdateModule(modview);
        Assert.NotNull(mod);
        Assert.NotNull(mod?.script);
    }

    [Fact]
    public void BasicParameterPass()
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                return ""Id: "" .. uid .. "" Data: "" .. data
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "whatever", 8); //new Requester() {userId = 8});
        Assert.Equal("Id: 8 Data: whatever", result);
    }

    [Fact]
    public void WrongSubcommands()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = ""wow""
            function default(uid, data)
                return ""Id: "" .. uid .. "" Data: "" .. data
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "whatever", 8); //new Requester() {userId = 8});
        Assert.Equal("Id: 8 Data: whatever", result);
    }

    [Fact]
    public void EmptySubcommand()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={} }
            function command_wow(uid, data)
                return ""Id: "" .. uid .. "" Data: "" .. data
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "wow whatever", 8); //new Requester() {userId = 8});
        Assert.Equal("Id: 8 Data: whatever", result);
    }

    [Fact]
    public void SubcommandFunction()
    {
        //The subcommands variable exists but has no argument list; we should still be able to redefine the function
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={[""function""]=""lolwut""} }
            function lolwut(uid, data)
                return ""Id: "" .. uid .. "" Data: "" .. data
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", " wow  whatever ", 8); //new Requester() {userId = 8});
        Assert.Equal("Id: 8 Data: whatever", result);
    }

    [Fact]
    public void SubcommandArguments_Word()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={[""arguments""]={""first_word"",""second_word""}} }
            function command_wow(uid, word1, word2)
                return ""Id: "" .. uid .. "" Word1: "" .. word1 .. "" Word2: "" .. word2
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "wow whatever stop", 8); //new Requester() {userId = 8});
        Assert.Equal("Id: 8 Word1: whatever Word2: stop", result);
    }

    [Fact]
    public void EmptySubcommandKey()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""""]={[""arguments""]={""first_word"",""second_word""}} }
            function command_(uid, word1, word2)
                return ""Id: "" .. uid .. "" Word1: "" .. word1 .. "" Word2: "" .. word2
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "whatever stop", 8); //new Requester() {userId = 99});
        Assert.Equal("Id: 99 Word1: whatever Word2: stop", result);
    }

    [Fact]
    public async Task SubcommandArguments_User()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={[""arguments""]={""first_user"",""second_user""}} }
            function command_wow(uid, user1, user2)
                return ""Id: "" .. uid .. "" User1: "" .. user1 .. "" User2: "" .. user2
            end" 
        };
        //Fragile test, should inject a fake user service that always says the user is good. oh well
        var user1 = await searcher.GetById<UserView>(RequestType.user, (int)UserVariations.Super);
        var user2 = await searcher.GetById<UserView>(RequestType.user, 1 + (int)UserVariations.Super);
        //userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
        //userService.WriteAsync(new UserViewFull() { username = "dude2"}, new Requester() { system = true }).Wait();
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", $"wow {user1.id} {user2.id}(lol_username!)", 8);
        Assert.Equal($"Id: 8 {user1.username}: {user1.id} {user2.username}: {user2.id}", result);
    }

    [Fact]
    public async Task SubcommandArguments_Mixed()
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={[""arguments""]={""first_user"",""second_word"",""third_freeform""}} }
            function command_wow(uid, user, word, freeform)
                return ""Id: "" .. uid .. "" User: "" .. user .. "" Word: "" .. word .. "" Freeform: "" .. freeform
            end" 
        };
        var user1 = await searcher.GetById<UserView>(RequestType.user, (int)UserVariations.Super);
        //userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", $"wow {user1.id}(somebody) kills a lot of people", 8); //new Requester() {userId = 8});
        Assert.Equal($"Id: 8 User: {user1.id} Word: kills Freeform: a lot of people", result);
    }

    [Theory]
    [InlineData(0, "Moments ago")]
    [InlineData(30, "30 seconds ago")]
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
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, time)
                return timesincetimestamp(time)
            end" 
        };
        //userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", DateTime.Now.Subtract(TimeSpan.FromSeconds(subtractSeconds)).ToString(), 8); //new Requester() {userId = 8});
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
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                setdata(""key"", data)
                return getdata(""key"")
            end" 
        };
        //userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", data, 8); //new Requester() {userId = 8});
        Assert.Equal(data, result);
    }

    [Theory]
    [InlineData("word", "abc", "string")]
    [InlineData("word", "123", "string")]
    [InlineData("int", "123", "number")]
    public void ArgTyping(string type, string data, string expected)
    {
        //The subcommands variable exists but is the wrong type, the module system shouldn't care
        var modview = new ContentView() { name = "test", text = @"
            subcommands = {[""wow""]={[""arguments""]={""first_" + type + @"""}} }
            function command_wow(uid, data)
                return type(data)
            end" 
        };
        //userService.WriteAsync(new UserViewFull() { username = "dude1"}, new Requester() { system = true }).Wait();
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "wow " + data, 8); //new Requester() {userId = 8});
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("First test")]
    [InlineData("Second test", "Another line")]
    [InlineData("", "And then!!", "OMG SO MUCH LOGGING")]
    public void PrntDbg(params string[] allmessages)
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                prntdbg(""Logging here!"")
            end" 
        };
        var mod = service.UpdateModule(modview) ?? throw new InvalidOperationException("NO MODULE RETURNED");
        foreach(var message in allmessages)
            service.RunCommand("test", message, 8); //new Requester() {userId = 8});

        Assert.Equal(allmessages.Length, mod.debug.Count);
        for(int i = 0; i < allmessages.Length; i++)
            Assert.Equal($"[8:default|{allmessages[i]}] Logging here!", mod.debug.ElementAt(i).Substring(0, mod.debug.ElementAt(i).IndexOf("(") - 1));
    }

    [Fact]
    public void BasicDataReadWrite()
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                setdata(""myval"", ""something"")
                return getdata(""myval"")
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "whatever", 8); //new Requester() {userId = 8});
        Assert.Equal("something", result);
    }

    [Theory]
    [InlineData("Just a regular string")]
    [InlineData("But NOW, we have the dreaded /")]
    [InlineData("// all day / baybe //")]
    [InlineData("OK but then / yeah")]
    [InlineData("You. Really/really?! Need == to !!! !/!/! ANCD091823 and then / \\ \" '' /' badstuff")]
    [InlineData("😜😋 hahaha 16 bit nope 🤨🤙")]
    [InlineData("t̵̡̧̲̥̘̀͗͐̂i̶̡̛̮̦̝̳̾̊͐̉̌͊̓͘͘͝m̵̬̔͂̾̌̽e̷̢̧͇̬̩͎͙̼͕̻̘̳͖͇̙̊̈̅͑̄͑̒̎̓̑͂͋͋͋͝ ̵̡̙̪̪̫̬̮͇͎̝̆̓̔̅̀t̶͎͊̒̄̍̓̾ǫ̶̳͚̝̺͔͔̘̦̤̤̩͕͓̈̉͋͋̓̔̃̐̕ͅ ̷̡̖̘͈̭̞͖̯̗̗̹͊̈́͆̽̉͝ͅf̶̨̛͓͓̦̘̖̟͇̦̩͔̰͇̆͂̊́̓͗͜r̶̨̘̹̻͚̰͈̘͔̲͙̂͛̔̊̅̔̈́̂͌̔̾͠ͅͅy̶̢̡̥̜̠̗͍͔̓")]
    public void Base64Transparent(string test)
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                return b64decode(b64encode(data))
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", test, 8); //new Requester() {userId = 8});
        Assert.Equal(test, result);
    }

    [Theory]
    [InlineData("Just a regular string")]
    [InlineData("But NOW, we have the dreaded /")]
    [InlineData("// all day / baybe //")]
    [InlineData("OK but then / yeah")]
    [InlineData("You. Really/really?! Need == to !!! !/!/! ANCD091823 and then / \\ \" '' /' badstuff")]
    [InlineData("😜😋 hahaha 16 bit nope 🤨🤙")]
    [InlineData("t̵̡̧̲̥̘̀͗͐̂i̶̡̛̮̦̝̳̾̊͐̉̌͊̓͘͘͝m̵̬̔͂̾̌̽e̷̢̧͇̬̩͎͙̼͕̻̘̳͖͇̙̊̈̅͑̄͑̒̎̓̑͂͋͋͋͝ ̵̡̙̪̪̫̬̮͇͎̝̆̓̔̅̀t̶͎͊̒̄̍̓̾ǫ̶̳͚̝̺͔͔̘̦̤̤̩͕͓̈̉͋͋̓̔̃̐̕ͅ ̷̡̖̘͈̭̞͖̯̗̗̹͊̈́͆̽̉͝ͅf̶̨̛͓͓̦̘̖̟͇̦̩͔̰͇̆͂̊́̓͗͜r̶̨̘̹̻͚̰͈̘͔̲͙̂͛̔̊̅̔̈́̂͌̔̾͠ͅͅy̶̢̡̥̜̠̗͍͔̓")]
    public void Base64JsonTransparent(string test)
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                local tbl = { [""thing""] = data }
                tbl.thing = b64encode(tbl.thing)
                local serial = json.serialize(tbl)
                local tbl2 = json.parse(serial)
                return b64decode(tbl2.thing)
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", test, 8); //new Requester() {userId = 8});
        Assert.Equal(test, result);
    }

    [Fact]
    public void SecondDataReadWrite()
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                if data != nil then
                    setdata(""myval"", data)
                end
                return getdata(""myval"")
            end" 
        };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "something", 8); //new Requester() {userId = 8});
        Assert.Equal("something", result);
        result = service.RunCommand("test", null, 8); //new Requester() {userId = 8});
        Assert.Equal("something", result);
    }

    [Fact]
    public void ReadMessagesInstant()
    {
        var modview = new ContentView() { name = "test", text = @"
            function default(uid, data)
                usermessage(uid, ""hey"")
                usermessage(uid + 1, ""hey NO"")
            end" 
        };
        //var requester = new Requester() { userId = 9 };
        var mod = service.UpdateModule(modview);
        var result = service.RunCommand("test", "whatever", 9); //requester);
        var messages = searcher.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest()
        {
            type = "message",
            query = "!notnull(module)"
        }).Result;
         //moduleMessageService.SearchAsync(new ModuleMessageViewSearch(), requester).Result; //service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
        Assert.Single(messages);
        Assert.Equal("hey", messages.First().text);
        Assert.Equal("test", messages.First().module);
        //Assert.Equal(requester.userId, messages.First().receiveUserId);
        //Assert.Equal(requester.userId, messages.First().sendUserId);
        Assert.Equal(9, messages.First().receiveUserId);
        Assert.Equal(9, messages.First().createUserId);
    }

    //[Fact]
    //public void ReadRoomMessagesInstant()
    //{
    //    var modview = new ContentView() { name = "test", code = @"
    //        function default(uid, data)
    //            broadcastmessage(""hey"")
    //            broadcastmessage(uid + 1, ""hey NO"")
    //        end" 
    //    };
    //    var requester = new Requester() { userId = 9 };
    //    var mod = service.UpdateModule(modview);
    //    var result = service.RunCommand("test", "whatever", requester, );
    //    var messages = moduleMessageService.SearchAsync(new ModuleMessageViewSearch(), requester).Result; //service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
    //    Assert.Single(messages);
    //    Assert.Equal("hey", messages.First().message);
    //    Assert.Equal("test", messages.First().module);
    //    Assert.Equal(requester.userId, messages.First().receiveUserId);
    //    Assert.Equal(requester.userId, messages.First().sendUserId);
    //}

}