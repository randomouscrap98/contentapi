using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

//WARN: ALL TESTS THAT ACCESS THE SEARCHFIXTURE SHOULD GO IN HERE! Otherwise the database
//will be created for EVERY class that uses the fixture, increasing the test time! Just
//keep it together, even if the class gets large!
public class GenericSearchDbTests : UnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    //protected IDbConnection dbcon;
    protected GenericSearcher service;
    protected DbUnitTestSearchFixture fixture;

    public GenericSearchDbTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        var conWrap = fixture.GetService<ContentApiDbConnection>();
        service = new GenericSearcher(fixture.GetService<ILogger<GenericSearcher>>(), 
            conWrap, fixture.GetService<ITypeInfoService>(), fixture.GetService<GenericSearcherConfig>(),
            fixture.GetService<IMapper>(), fixture.GetService<IQueryBuilder>());
        //dbcon = conWrap.Connection;
    }

    [Fact]
    public void GenericSearch_ConnectionSuccessful()
    {
        //If THIS fails, it'll be because you don't have the services or database set up 
        //correctly, and thus that needs to be fixed before any other tests in here are looked at
        //Assert.NotNull(dbcon);
        Assert.NotNull(service);
    }

    [Fact]
    public void GenericSearch_Search_AllFields()
    {
        foreach(var type in Enum.GetNames<RequestType>())
        {
            var search = new SearchRequests();
            search.requests.Add(new SearchRequest()
            {
                name = "testStar",
                type = type,
                fields = "*", //THIS is what we're testing
            });

            var result = service.SearchUnrestricted(search).Result.data["testStar"];
            Assert.NotEmpty(result);

            //Here, we're just making sure that "*" didn't break anything. We assume
            //that "*" is implemented generically, and thus we can do some other test
            //some other time for whether all fields are returned, but that is not 
            //necessary for this broad test
        }
    }

    protected async Task GetByIdBasicTest<T>(RequestType type, long id) where T : IIdView
    {
        var result = await service.GetById<T>(type, id); //The first content is just a content
        Assert.Equal(id, result.id);
        //We assume the searcher is using the same system as other searches, so don't need to super test everything
    }

    [Fact] public async Task GenericSearch_GetById_BasicContent() => await GetByIdBasicTest<ContentView>(RequestType.content, 1 + (int)InternalContentType.none);
    [Fact] public async Task GenericSearch_GetById_BasicFile() => await GetByIdBasicTest<FileView>(RequestType.file, 1 + (int)InternalContentType.file);
    [Fact] public async Task GenericSearch_GetById_BasicPage() => await GetByIdBasicTest<PageView>(RequestType.page, 1 + (int)InternalContentType.page);
    [Fact] public async Task GenericSearch_GetById_BasicModule() => await GetByIdBasicTest<ModuleView>(RequestType.module, 1 + (int)InternalContentType.module);
    [Fact] public async Task GenericSearch_GetById_BasicComment() => await GetByIdBasicTest<CommentView>(RequestType.comment, 1);
    [Fact] public async Task GenericSearch_GetById_BasicActivity() => await GetByIdBasicTest<ActivityView>(RequestType.activity, 1);
    [Fact] public async Task GenericSearch_GetById_BasicUser() => await GetByIdBasicTest<UserView>(RequestType.user, 1);
    [Fact] public async Task GenericSearch_GetById_BasicWatch() => await GetByIdBasicTest<WatchView>(RequestType.watch, 1);

    [Fact]
    public async Task GenericSearch_GetById_Deleted()
    {
        var result = await service.GetById<ContentView>(RequestType.content, 1 + (int)ContentVariations.Deleted);
        Assert.True(result.deleted);
        Assert.Equal(1 + (int)ContentVariations.Deleted, result.id);
    }

    [Fact]
    public async Task GenericSearch_GetById_Deleted_NotFound()
    {
        await Assert.ThrowsAnyAsync<NotFoundException>(async () => await service.GetById<ContentView>(RequestType.content, 1 + (int)ContentVariations.Deleted, true));
    }

    [Fact]
    public async Task GenericSearch_GetById_NotFound()
    {
        await Assert.ThrowsAnyAsync<NotFoundException>(async () => await service.GetById<ContentView>(RequestType.content, 2 + fixture.ContentCount));
    }

    //This ALSO TESTS parts of the usertype macro!
    [Fact]
    public async Task GenericSearch_ToStronglyTyped_User()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "user",
            fields = "*", //THIS is what we're testing
            query = "!usertype(user)"
        });

        var result = (await service.SearchUnrestricted(search)).data["test"];
        var castResult = service.ToStronglyTyped<UserView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.username), "Username wasn't cast properly!");
            Assert.True(x.id > 0, "UserID not cast properly!");
            Assert.False(string.IsNullOrWhiteSpace(x.avatar), "UserAvatar not cast properly!");
            Assert.True(x.createDate.Ticks > 0, "User createdate not cast properly!");

            //In our system, all users are assigned to at least one group for testing
            Assert.NotEmpty(x.groups);
            //The group should be evenly split
            Assert.Equal(fixture.UserCount + fixture.GroupCount * (x.id - 1) / fixture.UserCount, x.groups.First() - 1);
        });

        Assert.Equal(fixture.UserCount / 2, castResult.Where(x => x.registered).Count());
        Assert.Equal(fixture.UserCount / 2, castResult.Where(x => x.super).Count());
        Assert.Equal(fixture.UserCount / 2, castResult.Where(x => x.special != null).Count());
    }

    //This tests both content automapping AND whether or not the content fields are actually pulled...
    [Fact]
    public async Task GenericSearch_ToStronglyTyped_Content_ExtraFields()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "content",
            fields = "*", //THIS is what we're testing
        });

        var result = (await service.SearchUnrestricted(search)).data["test"];
        var castResult = service.ToStronglyTyped<ContentView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.name), "Content name wasn't cast properly!");
            //Assert.NotEqual(InternalContentType.none, x.internalType); //string.IsNullOrWhiteSpace(x.internalType), "Content internalType (string) wasn't cast properly!");
            if(((x.id - 1) & (int)ContentVariations.Comments) > 0)
                Assert.Equal(x.id - 1, x.commentCount);
            else
                Assert.Equal(0, x.commentCount);
            Assert.Equal((x.id - 1) / (fixture.ContentCount / fixture.UserCount), x.watchCount);
            Assert.Equal((x.id - 1) / (fixture.ContentCount / fixture.UserCount), x.votes.Sum(x => x.Value));
            Assert.True(x.id > 0, "ContentId not cast properly!");
            Assert.True(x.createUserId > 0, "Content createuserid not cast properly!");
            Assert.True(x.createDate.Ticks > 0, "Content createdate not cast properly!");
        });

        Assert.Equal(4, Enum.GetValues<InternalContentType>().Count());
        foreach(var type in Enum.GetValues<InternalContentType>())
            Assert.Equal(fixture.ContentCount / 4, castResult.Where(x => x.internalType == type).Count());
        Assert.Equal(fixture.ContentCount / 2, castResult.Where(x => x.deleted).Count());
        Assert.Equal(fixture.ContentCount - 4, castResult.Where(x => x.parentId > 0).Count());
        //It's minus four because the parent id is actually divided by 4, so only the first 4 values will be 0
        //(the parentID is based on the iterator, which starts at 0)

        //These two test whether or not the system pulls in the extra values!!
        Assert.Equal(fixture.ContentCount / 2, castResult.Where(x => x.keywords.Count > 0).Count());
        Assert.Equal(fixture.ContentCount / 2, castResult.Where(x => x.values.Count > 0).Count());
        Assert.Equal(fixture.ContentCount / 2, castResult.Where(x => x.permissions.Where(x => x.Key == 0).Count() > 0).Count());
        Assert.Equal(fixture.ContentCount / 2, castResult.Where(x => x.permissions.Where(x => x.Key != 0 && x.Key <= fixture.UserCount && ((x.Key - 1) & (int)UserVariations.Super) > 0).Count() > 0).Count());
        //Note for supers: the super permission given to each content is the content id with the user super bit
        //set modulo the user count. Thus, basically, the keys should all have the super bit set. But, subtract one
        //because database IDs are plus one.
    }

    [Fact]
    public async Task GenericSearch_ToStronglyTyped_Activity()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "activity",
            fields = "*"
        });

        var result = (await service.SearchUnrestricted(search)).data["test"];
        var castResult = service.ToStronglyTyped<ActivityView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            //Assert.False(string.IsNullOrWhiteSpace(x.action), "Action wasn't cast properly!");
            Assert.True(x.action == UserAction.update || x.action == UserAction.delete, "Action wasn't an expected value!");
            Assert.True(x.id > 0, "ActivityID not cast properly!");
            Assert.True(x.userId > 0, "UserId not cast properly!");
            Assert.True(x.contentId > 0, "ContentID not cast properly!");
            Assert.True(x.date.Ticks > 0, "Activity date not cast properly!");
        });
    }

    [Fact]
    public async Task GenericSearch_ToStronglyTyped_AdminLog()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "adminlog",
            fields = "*"
        });

        var result = (await service.SearchUnrestricted(search)).data["test"];
        var castResult = service.ToStronglyTyped<AdminLogView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.text), "Adminlog text wasn't cast properly!");
            Assert.Contains(x.type, Enum.GetValues<AdminLogType>());
            Assert.True(x.id > 0, "Adminlog ID not cast properly!");
            Assert.True(x.initiator > 0, "Adminlog initiator not cast properly!");
            Assert.True(x.target > 0, "AminLog target not cast properly!");
            Assert.True(x.createDate.Ticks > 0, "AdminLog date not cast properly!");
        });
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData((int)UserVariations.Super, false)]
    [InlineData(1 + (int)UserVariations.Super, true)]
    public async Task GenericSearch_AdminLog_Gettable(long userId, bool expectResults)
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "adminlog",
            fields = "*"
        });

        if(expectResults)
        {
            var result = (await service.Search(search, userId)).data["test"];
            Assert.Equal(fixture.AdminLogCount, result.Count());
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async() =>
            {
                var result = (await service.Search(search, userId)).data["test"];
            });
        }
    }
    
    [Fact]
    public async Task GenericSearch_UsersAndGroupsTogether()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "user",
            fields = "*"
        });

        //Both should be castable to a userview
        var result = (await service.SearchUnrestricted(search)).data["test"];
        var castResult = service.ToStronglyTyped<UserView>(result);

        Assert.Equal(fixture.UserCount + fixture.GroupCount, castResult.Count);
        Assert.Equal(fixture.UserCount, castResult.Count(x => x.type == UserType.user));
        Assert.Equal(fixture.GroupCount, castResult.Count(x => x.type == UserType.group));
    }

    [Fact]
    public void GenericSearch_Search_FieldLimiting()
    {
        var search = new SearchRequests();
        //search.values.Add("userlike", "admin%");
        search.requests.Add(new SearchRequest()
        {
            name = "fieldLimit",
            type = "user",
            fields = "id, username",
            //query = "username like @userlike" //Don't need to test syntax btw! Already done!
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.All(result["fieldLimit"], x => {
            Assert.Equal(2, x.Keys.Count);
            Assert.Contains("id", x.Keys);
            Assert.Contains("username", x.Keys);
        });
    }

    [Fact]
    public void GenericSearch_Search_LessThan()
    {
        var search = new SearchRequests();
        search.values.Add("maxid", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "lessthan",
            type = "content",
            fields = "id",
            query = "id < @maxid" //Don't need to test syntax btw! Already done!
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Equal(9, result["lessthan"].Count());
    }

    [Fact]
    public void GenericSearch_Search_LessThanEqual()
    {
        var search = new SearchRequests();
        search.values.Add("maxid", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "lessthanequal",
            type = "content",
            fields = "id",
            query = "id <= @maxid"
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Equal(10, result["lessthanequal"].Count());
    }

    [Fact]
    public void GenericSearch_Search_GreaterThan()
    {
        var search = new SearchRequests();
        search.values.Add("minid", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "greaterthan",
            type = "content",
            fields = "id",
            query = "id > @minid" //Don't need to test syntax btw! Already done!
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Equal(fixture.ContentCount - 10, result["greaterthan"].Count());
    }

    [Fact]
    public void GenericSearch_Search_GreaterThanEqual()
    {
        var search = new SearchRequests();
        search.values.Add("minid", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "greaterthanequal",
            type = "content",
            fields = "id",
            query = "id >= @minid" //Don't need to test syntax btw! Already done!
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Equal(fixture.ContentCount - 9, result["greaterthanequal"].Count());
    }

    [Fact]
    public void GenericSearch_Search_Equal()
    {
        var search = new SearchRequests();
        search.values.Add("id", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "equal",
            type = "content",
            fields = "id",
            query = "id = @id"
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Single(result["equal"]);
        Assert.Equal(10L, result["equal"].First()["id"]);
    }

    [Fact]
    public void GenericSearch_Search_NotEqual()
    {
        var search = new SearchRequests();
        search.values.Add("id", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "noequal", //can't have keywords in names for now, oops
            type = "content",
            fields = "id",
            query = "id <> @id"
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Equal(fixture.ContentCount - 1, result["noequal"].Count());
        Assert.All(result["noequal"], x =>
        {
            //Make sure that one we didn't want wasn't included
            Assert.NotEqual(10L, x["id"]);
        });
    }

    [Fact]
    public void GenericSearch_Search_Like()
    {
        var search = new SearchRequests();
        search.values.Add("contentlike", "content_1%");
        search.requests.Add(new SearchRequest()
        {
            name = "testlike",
            type = "content",
            fields = "id, name",
            query = "name like @contentlike"
        });

        var result = service.SearchUnrestricted(search).Result.data["testlike"];
        Assert.True(fixture.ContentCount >= 20); //This test only works when there's a lot of content
        Assert.Equal(11, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
    }

    [Fact]
    public void GenericSearch_Search_NotLike()
    {
        var search = new SearchRequests();
        search.values.Add("contentnotlike", "content_1%");
        search.requests.Add(new SearchRequest()
        {
            name = "testnotlike",
            type = "content",
            fields = "id",
            query = "name not like @contentnotlike"
        });

        var result = service.SearchUnrestricted(search).Result.data["testnotlike"];
        Assert.True(fixture.ContentCount >= 20); //This test only works when there's a lot of content
        Assert.Equal(fixture.ContentCount - 11, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
    }

    [Fact]
    public void GenericSearch_Search_In()
    {
        var search = new SearchRequests();
        search.values.Add("ids", new int[] { 1, 10, 15 });
        search.requests.Add(new SearchRequest()
        {
            name = "idin",
            type = "content",
            fields = "id",
            query = "id in @ids"
        });

        var result = service.SearchUnrestricted(search).Result.data["idin"];
        Assert.Equal(3, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
        Assert.Contains(1L, result.Select(x => x["id"]));
        Assert.Contains(10L, result.Select(x => x["id"]));
        Assert.Contains(15L, result.Select(x => x["id"]));
    }

    [Fact]
    public void GenericSearch_Search_NotIn()
    {
        var search = new SearchRequests();
        search.values.Add("ids", new int[] { 1, 10, 15 });
        search.requests.Add(new SearchRequest()
        {
            name = "idnotin",
            type = "content",
            fields = "id",
            query = "id not in @ids"
        });

        var result = service.SearchUnrestricted(search).Result.data["idnotin"];
        Assert.Equal(fixture.ContentCount - 3, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
        Assert.DoesNotContain(1L, result.Select(x => x["id"]));
        Assert.DoesNotContain(10L, result.Select(x => x["id"]));
        Assert.DoesNotContain(15L, result.Select(x => x["id"]));
    }

    [Fact]
    public void GenericSearch_Search_Autoname() //A newer feature, name is just type
    {
        var search = new SearchRequests();
        search.values.Add("id", 10);
        search.requests.Add(new SearchRequest()
        {
            type = "content",
            fields = "id",
            query = "id = @id"
        });

        var result = service.SearchUnrestricted(search).Result.data;

        Assert.Single(result["content"]);
        Assert.Equal(10L, result["content"].First()["id"]);
    }

    [Fact]
    public async Task GenericSearch_Search_FailGracefully_NameValueCollision()
    {
        //The exact setup that produced the failure, oops
        var search = new SearchRequests();
        search.values.Add("idin", new int[] { 1, 10, 15 });
        search.requests.Add(new SearchRequest()
        {
            name = "idin",
            type = "user",
            fields = "id",
            query = "id in @idin"
        });

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var result = await service.SearchUnrestricted(search);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_FailGracefully_DuplicateNames()
    {
        //The exact setup that produced the failure, oops
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "idin",
            type = "user",
            fields = "id"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "idin",
            type = "content",
            fields = "id"
        });

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var result = await service.SearchUnrestricted(search);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_SimpleLink()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "allpages",
            type = "content",
            fields = "id, name, createUserId, createDate",
        });
        search.requests.Add(new SearchRequest()
        {
            name = "createusers",
            type = "user",
            fields = "id, username, special, avatar",
            query = "id in @allpages.createUserId"
        });

        var result = (await service.SearchUnrestricted(search)).data;
        Assert.Contains("allpages", result.Keys);
        Assert.Contains("createusers", result.Keys);
        Assert.Equal(fixture.ContentCount, result["allpages"].Count());

        Assert.All(result["allpages"], x =>
        {
            Assert.Contains(x["createUserId"], result["createusers"].Select(x => x["id"]));
        });

        Assert.All(result["createusers"], x =>
        {
            Assert.Contains(x["id"], result["allpages"].Select(x => x["createUserId"]));
        });
    }

    [Fact]
    public async Task GenericSearch_Search_BasicFieldNotRequired()
    {
        var search = new SearchRequests();
        search.values.Add("userlike", "user_%");
        search.requests.Add(new SearchRequest()
        {
            name = "basicfield",
            type = "user",
            fields = "id", //Even though username is not there, we should be able to query for it
            query = "username like @userlike"
        });

        var result = (await service.SearchUnrestricted(search)).data["basicfield"];
        Assert.Equal(fixture.UserCount, result.Count());
    }

    [Fact]
    public async Task GenericSearch_Search_RemappedField_Searchable()
    {
        //This test relies on the amount of content types. If it changes, just fix it, it's easy
        Assert.Equal(4, Enum.GetValues<InternalContentType>().Count());

        var search = new SearchRequests();
        search.values.Add("hash", fixture.StandardPublicTypes[(int)InternalContentType.file]);
        search.requests.Add(new SearchRequest()
        {
            name = "complex",
            type = "file",
            fields = "id, hash", 
            query = "hash = @hash"
        });

        var result = (await service.SearchUnrestricted(search)).data["complex"];
        Assert.Equal(fixture.ContentCount / 4 / 2, result.Count());
    }

    [Fact]
    public async Task GenericSearch_Search_RemappedField_FailGracefully()
    {
        var search = new SearchRequests();
        search.values.Add("hash", "one");
        search.requests.Add(new SearchRequest()
        {
            name = "nocomplex",
            type = "file",
            fields = "id", //Only querying id, but asking for hash, which needs to be included
            query = "hash = @hash"
        });

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var result = await service.SearchUnrestricted(search);
        });
    }


    [Fact]
    public async Task GenericSearch_Search_LexerKeywordPrefix()
    {
        var search = new SearchRequests();

        var keywords = new[] { "and", "or", "not", "in", "like" };

        foreach(var k in keywords)
        {
            search.values.Add($"{k}ids", new int[] { 1, 10, 15 });
            search.requests.Add(new SearchRequest()
            {
                name = $"{k}test",
                type = "content",
                fields = "id",
                query = $"id not in @{k}ids"
            });
        }
        //Here, I'm using a keyword as the start of different names. This would normally fail
        //in the regular lexer, but with the additions, it will allow this to work(?)

        var result = (await service.SearchUnrestricted(search)).data;
        foreach(var k in keywords)
        {
            var r = result[$"{k}test"];
            Assert.Equal(fixture.ContentCount - 3, r.Count());
            Assert.DoesNotContain(1L, r.Select(x => x["id"]));
            Assert.DoesNotContain(10L, r.Select(x => x["id"]));
            Assert.DoesNotContain(15L, r.Select(x => x["id"]));
        }
    }

    [Fact]
    public async Task GenericSearch_Search_PermissionDefault()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "permissiondefault",
            type = "content",
            fields = "*"
        });

        var result = (await service.Search(search)).data["permissiondefault"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        //Because the permission thing "all or none" is just based on a bit, it will
        //always be HALF of the content that we're allowed to get. The ID should also
        //be related to the ones that have it.
        Assert.Equal(fixture.ContentCount / 2, result.Count());
        Assert.All(castResult, x =>
        {
            //Minus 1 because the database ids start at 1
            Assert.True(((x.id - 1) & (int)ContentVariations.AccessByAll) > 0);
            Assert.Contains(0, x.permissions.Keys);
            Assert.Contains("R", x.permissions[0]);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericSearch_Search_PermissionGroup(bool allContent)
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "permission",
            type = "content",
            fields = "*"
        });

        //Any user in the last group should have access to EVERY content! We know the very last user is in the last group
        //because of how it works out! We can also verify that the first user, who is definitely NOT in the last group,
        //has permissions only for half the content, just like the default user
        if(allContent)
        {
            //Has to be a user that doesn't already have access, which would be the last non-super user
            var result = (await service.Search(search, 1 + ((fixture.UserCount - 1) & ~((int)UserVariations.Super)))).data["permission"];
            Assert.Equal(fixture.ContentCount, result.Count());
        }
        else
        {
            var uid = 1;
            var result = (await service.Search(search, uid)).data["permission"];
            var casted = service.ToStronglyTyped<ContentView>(result);
            Assert.All(casted, x => {
                //They're not in "the group", so they are either the creator OR the permissions have them directly in it
                Assert.True(x.createUserId == uid || x.permissions.ContainsKey(uid) || x.permissions.ContainsKey(0), $"Found invalid content {x.id} for user {uid}, createuser: {x.createUserId}");
            });
        }
    }

    [Theory]
    [InlineData("C", true)]
    [InlineData("R", true)]
    [InlineData("U", true)]
    [InlineData("D", true)]
    [InlineData("butts", false)]
    public async Task GenericSearch_Search_PermissionMacro(string immediate, bool success)
    {
        var search = new SearchRequests();
        search.values.Add("users", new[] { 0 });
        search.requests.Add(new SearchRequest()
        {
            name = "permissionmacro",
            type = "content",
            fields = "id",
            query = $"!permissionlimit(@users, id, {immediate})"
        });

        if(success)
        {
            var result = (await service.SearchUnrestricted(search)).data["permissionmacro"];
            Assert.Equal(fixture.ContentCount / 2, result.Count());
        }
        else
        {
            await Assert.ThrowsAnyAsync<ParseException>(async () => {
                var result = (await service.SearchUnrestricted(search)).data["permissionmacro"];
            });
        }
    }

    [Fact]
    public async Task GenericSearch_Search_InGroupMacro()
    {
        for(var i = 0; i < fixture.GroupCount; i++)
        {
            var search = new SearchRequests();
            search.values.Add("group", fixture.UserCount + i + 1);
            search.requests.Add(new SearchRequest()
            {
                name = "ingroupmacro",
                type = "user",
                fields = "*",
                query = "!ingroup(@group)"
            });

            var result = (await service.SearchUnrestricted(search)).data["ingroupmacro"];
            var castResult = service.ToStronglyTyped<UserView>(result);

            Assert.All(castResult, x => {
                Assert.Equal(UserType.user, x.type); //We're asking for USERS in groups only
                Assert.True((x.id - 1) >= i * fixture.UserCount / fixture.GroupCount && (x.id - 1) < (i + 1) * fixture.UserCount / fixture.GroupCount, $"Unexpected user {x.id} for group {i}");
            });
        }
    }

    [Fact]
    public async Task GenericSearch_Search_KeywordMacro()
    {
        var search = new SearchRequests();
        search.values.Add("search", "one");
        search.requests.Add(new SearchRequest()
        {
            name = "keywordmacro",
            type = "content",
            fields = "id",
            query = "!keywordlike(@search)"
        });

        var result = (await service.SearchUnrestricted(search)).data["keywordmacro"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        //At least half the content should have the "one" keyword, and they should all have
        //an id with the keyword flag set
        Assert.Equal(fixture.ContentCount / 2, result.Count());
        Assert.All(castResult, x =>
        {
            Assert.True(((x.id - 1) & (int)ContentVariations.Keywords) > 0);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_KeywordMacro_None()
    {
        var search = new SearchRequests();
        search.values.Add("search", "%NOBODY%");
        search.requests.Add(new SearchRequest()
        {
            name = "keywordmacro",
            type = "content",
            fields = "id",
            query = "!keywordlike(@search)"
        });

        var result = (await service.SearchUnrestricted(search)).data["keywordmacro"];
        var castResult = service.ToStronglyTyped<ContentView>(result);
        //This also secretly tests if ToStronglyTyped accepts blanks
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_ValueMacro()
    {
        var search = new SearchRequests();
        search.values.Add("key", "contentval_%");
        search.values.Add("search", "value_%");
        search.requests.Add(new SearchRequest()
        {
            name = "valuemacro",
            type = "content",
            fields = "id",
            query = "!valuelike(@key, @search)"
        });

        var result = (await service.SearchUnrestricted(search)).data["valuemacro"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        //At least half the content should have the "one" keyword, and they should all have
        //an id with the keyword flag set
        Assert.Equal(fixture.ContentCount / 2, result.Count());
        Assert.All(castResult, x =>
        {
            Assert.True(((x.id - 1) & (int)ContentVariations.Values) > 0);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_ValueMacro_None()
    {
        var search = new SearchRequests();
        search.values.Add("key", "%nope%");
        search.values.Add("search", "value_%"); //even though this one is fine
        search.requests.Add(new SearchRequest()
        {
            name = "valuemacro",
            type = "content",
            fields = "id",
            query = "!valuelike(@key, @search)"
        });

        var result = (await service.SearchUnrestricted(search)).data["valuemacro"];
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_OnlyParentsMacro()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "parentmacro",
            type = "content",
            fields = "*",
            query = "!onlyparents()"
        });

        var result = (await service.SearchUnrestricted(search)).data["parentmacro"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        //Parents are assigned / 4, so there should be that many (minus 0 parents)
        Assert.Equal(fixture.ContentCount / 4 - 1, result.Count());
        Assert.All(castResult, x =>
        {
            Assert.True((x.id - 1) < fixture.ContentCount / 4);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_OnlyParentsMacro_Empty()
    {
        var search = new SearchRequests();
        search.values.Add("minId", fixture.ContentCount / 4 + 1);
        search.requests.Add(new SearchRequest()
        {
            name = "parentmacro",
            type = "content",
            fields = "*",
            query = "id > @minId and !onlyparents()"
        });

        var result = (await service.SearchUnrestricted(search)).data["parentmacro"];
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_BasicHistoryMacro()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "historymacro",
            type = "activity",
            fields = "*",
            query = "!basichistory()"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "actcon",
            type = "content",
            fields = "*",
            query = "id in @historymacro.contentId"
        });

        var result = (await service.SearchUnrestricted(search)).data;
        var castResult = service.ToStronglyTyped<ActivityView>(result["historymacro"]);
        var content = service.ToStronglyTyped<ContentView>(result["actcon"]);

        Assert.All(castResult, x =>
        {
            var c = content.First(y => y.id == x.contentId);
            Assert.False(c.deleted);
            Assert.Equal(InternalContentType.page, c.internalType);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_OnlyUserWatches()
    {
        //Do a NORMAL search with a requester
        for(var i = 1; i <= fixture.UserCount; i++)
        {
            //DON'T REUSE SEARCHES!!! DINGUS!!
            var search = new SearchRequests();
            search.requests.Add(new SearchRequest()
            {
                name = "watches",
                type = "watch",
                fields = "*"
            });

            var result = (await service.Search(search)).data["watches"];
            var castResult = service.ToStronglyTyped<ContentWatch>(result);

            //Make sure ALL the watches we got back are SPECIFICALLY for us, nobody else
            Assert.All(castResult, x =>
            {
                Assert.Equal(i, x.userId);
            });
        }
    }

    //This test tests a LOT of systems all at once! Does the macro system work?
    //Does the search system automatically limit, and does it do it correctly?
    //Can we actually retrieve the last post ID for all content while doing
    //all this other stuff?? This is the MOST like a regular user search! If this
    //is working correctly, chances are the whole system is at least MOSTLY working
    [Theory]
    [InlineData(0)]
    [InlineData((int)UserVariations.Super)] //Even though this is the user super bit, the UID is +1, so this means NOT super (confusingly)
    public async Task GenericSearch_StandardUseCase1(long uid)
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "allreadable",
            type = "page",
            fields = "*"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "createusers",
            type = "user",
            fields = "id, username, special, avatar",
            query = "id in @allreadable.createUserId"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "allcomments",
            type = "comment",
            fields = "id, text, contentId",
            query = "contentId in @allreadable.id"
        });

        //Get results as "default" user (meaning not logged in)
        var result = (await service.Search(search, uid)).data;

        Assert.Contains("allreadable", result.Keys);
        Assert.Contains("createusers", result.Keys);
        Assert.Contains("allcomments", result.Keys);

        var content = service.ToStronglyTyped<PageView>(result["allreadable"]);
        var users = service.ToStronglyTyped<UserView>(result["createusers"]);
        var comments = service.ToStronglyTyped<CommentView>(result["allcomments"]);

        Assert.All(content, x => 
        {
            Assert.Equal(InternalContentType.page, x.internalType);
            Assert.Contains(0, x.permissions.Keys);
            Assert.Contains("R", x.permissions[0]);
            Assert.True(users.Any(y => y.id == x.createUserId), "Didn't return matched content user!");
        });

        Assert.All(comments, x =>
        {
            Assert.True(content.Any(y => y.id == x.contentId), "returned comment not in content list!");
            Assert.True(users.Any(y => y.id == x.createUserId), "Didn't return matched comment user!");
        });
    }

    [Fact]
    public async Task GenericSearch_StandardUseCase2()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "allreadable",
            type = "content",
            fields = "id, name, createUserId, createDate, lastCommentId, permissions",
            query = "!notnull(lastCommentId)"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "lastcomments",
            type = "comment",
            fields = "*",
            query = "id in @allreadable.lastCommentId"
        });

        //Get results as "default" user (meaning not logged in)
        var result = (await service.Search(search)).data;

        Assert.Contains("allreadable", result.Keys);
        Assert.Contains("lastcomments", result.Keys);

        var content = service.ToStronglyTyped<ContentView>(result["allreadable"]);
        var comments = service.ToStronglyTyped<CommentView>(result["lastcomments"]);
        
        Assert.True(content.Count > 0, "There were no pages with comments somehow!");
        Assert.True(comments.Count > 0, "There were no comments somehow!");

        Assert.All(content, x => 
        {
            Assert.Contains(0, x.permissions.Keys);
            Assert.Contains("R", x.permissions[0]);
            if(x.lastCommentId != 0)
                Assert.True(comments.Any(y => y.id == x.lastCommentId), "Didn't return matched content last comment!");
        });

        Assert.All(comments, x =>
        {
            Assert.True(content.Any(y => y.id == x.contentId), "returned comment not in content list!");
        });
    }
}