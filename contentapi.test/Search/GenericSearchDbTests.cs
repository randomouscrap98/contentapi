using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Db;
using contentapi.Search;
using contentapi.data;
using contentapi.data.Views;
using Xunit;

namespace contentapi.test;

//WARN: ALL TESTS THAT ACCESS THE SEARCHFIXTURE SHOULD GO IN HERE! Otherwise the database
//will be created for EVERY class that uses the fixture, increasing the test time! Just
//keep it together, even if the class gets large!
[Collection("PremadeDatabase")]
public class GenericSearchDbTests : ViewUnitTestBase 
{
    protected GenericSearcher service;
    protected DbUnitTestSearchFixture fixture;

    public GenericSearchDbTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        service = fixture.GetGenericSearcher(); 
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
    public async Task GenericSearch_Search_AllFields()
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

            var result = await service.SearchUnrestricted(search);
            Assert.NotEmpty(result.objects["testStar"]);

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
    //[Fact] public async Task GenericSearch_GetById_BasicFile() => await GetByIdBasicTest<FileView>(RequestType.file, 1 + (int)InternalContentType.file);
    //[Fact] public async Task GenericSearch_GetById_BasicPage() => await GetByIdBasicTest<PageView>(RequestType.page, 1 + (int)InternalContentType.page);
    //[Fact] public async Task GenericSearch_GetById_BasicModule() => await GetByIdBasicTest<ModuleView>(RequestType.module, 1 + (int)InternalContentType.module);
    [Fact] public async Task GenericSearch_GetById_BasicComment() => await GetByIdBasicTest<MessageView>(RequestType.message, 1);
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

        var result = (await service.SearchUnrestricted(search)).objects["test"];
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
            expensive = true //NEED this to get ALL fields
        });

        var result = (await service.SearchUnrestricted(search)).objects["test"];
        var castResult = service.ToStronglyTyped<ContentView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.name), "Content name wasn't cast properly!");
            if(((x.id - 1) & (int)ContentVariations.Comments) > 0)
                Assert.Equal(x.id - 1, x.commentCount);
            else
                Assert.Equal(0, x.commentCount);
            Assert.Equal((x.id - 1) / (fixture.ContentCount / fixture.UserCount), x.watchCount);
            var voteCount = (x.id - 1) / (fixture.ContentCount / fixture.UserCount);
            if(voteCount == 0)
                Assert.False(x.engagement.ContainsKey(DbUnitTestSearchFixture.VoteEngagement));
            else
                Assert.Equal(voteCount, x.engagement[DbUnitTestSearchFixture.VoteEngagement].Sum(x => x.Value));
            Assert.True(x.id > 0, "ContentId not cast properly!");
            Assert.True(x.createUserId > 0, "Content createuserid not cast properly!");
            Assert.True(x.createDate.Ticks > 0, "Content createdate not cast properly!");
        });

        var contentTypeCount = Enum.GetValues<InternalContentType>().Count();
        //Assert.Equal(4, Enum.GetValues<InternalContentType>().Count());
        foreach(var type in Enum.GetValues<InternalContentType>())
        {
            var expectedCount = fixture.ContentCount / contentTypeCount;
            var resultCount = castResult.Where(x => x.contentType == type).Count();
            Assert.True(expectedCount <= resultCount);
        }
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

        var result = (await service.SearchUnrestricted(search)).objects["test"];
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

        var result = (await service.SearchUnrestricted(search)).objects["test"];
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

    [Fact]
    public async Task GenericSearch_ToStronglyTyped_UserVariable()
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            name = "test",
            type = "uservariable",
            fields = "*"
        });

        var result = (await service.SearchUnrestricted(search)).objects["test"];
        var castResult = service.ToStronglyTyped<UserVariableView>(result);
        Assert.NotEmpty(castResult);
        Assert.All(castResult, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.key), "UserVariable key wasn't cast properly!");
            Assert.False(string.IsNullOrWhiteSpace(x.value), "UserVariable value wasn't cast properly!");
            Assert.True(x.id > 0, "UserVariable ID not cast properly!");
            Assert.True(x.userId > 0, "UserVariable userId not cast properly!");
            Assert.True(x.createDate.Ticks > 0, "UserVariable createDate date not cast properly!");
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
            var result = (await service.Search(search, userId)).objects["test"];
            Assert.Equal(fixture.AdminLogCount, result.Count());
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async() =>
            {
                var result = (await service.Search(search, userId)).objects["test"];
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
        var result = (await service.SearchUnrestricted(search)).objects["test"];
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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = service.SearchUnrestricted(search).Result.objects["testlike"];
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
            fields = "id, name",
            query = "name not like @contentnotlike"
        });

        var result = service.SearchUnrestricted(search).Result.objects["testnotlike"];
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

        var result = service.SearchUnrestricted(search).Result.objects["idin"];
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

        var result = service.SearchUnrestricted(search).Result.objects["idnotin"];
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

        var result = service.SearchUnrestricted(search).Result.objects;

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

        var result = (await service.SearchUnrestricted(search)).objects;
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

    //TODO: this test was because we used to allow basic fields to be selected without actually selecting
    //them in the field list. This is no longer the case.
    //[Fact]
    //public async Task GenericSearch_Search_BasicFieldNotRequired()
    //{
    //    var search = new SearchRequests();
    //    search.values.Add("userlike", "user_%");
    //    search.requests.Add(new SearchRequest()
    //    {
    //        name = "basicfield",
    //        type = "user",
    //        fields = "id", //Even though username is not there, we should be able to query for it
    //        query = "username like @userlike"
    //    });

    //    var result = (await service.SearchUnrestricted(search)).data["basicfield"];
    //    Assert.Equal(fixture.UserCount, result.Count());
    //}

    [Fact]
    public async Task GenericSearch_Search_RemappedField_Searchable()
    {
        //This test relies on the amount of content types. If it changes, just fix it, it's easy
        //Assert.Equal(4, Enum.GetValues<InternalContentType>().Count());

        var search = new SearchRequests();
        //search.values.Add("hash", fixture.StandardPublicTypes[(int)InternalContentType.file]);
        //search.values.Add("type", InternalContentType.file);
        search.values.Add("registered", true);
        search.values.Add("usertype", UserType.user);
        search.requests.Add(new SearchRequest()
        {
            name = "complex",
            type = "user",
            //type = "content",
            fields = "id, registered, type", 
            //fields = "id, registered, contentType", 
            query = "registered = @registered and type = @usertype",
            //query = "hash = @hash and contentType = @type",
        });

        var result = (await service.SearchUnrestricted(search)).objects["complex"];
        Assert.Equal(fixture.UserCount / 2, result.Count());
    }

    //TODO: again, we outright deny all searches where the query field is not selected. This is a new restriction,
    //so these tests are no longer valid
    //[Fact]
    //public async Task GenericSearch_Search_RemappedField_NotIncluded_SimpleAllowed()
    //{
    //    var search = new SearchRequests();
    //    search.values.Add("hash", "four");
    //    search.requests.Add(new SearchRequest()
    //    {
    //        type = "file",
    //        fields = "id", 
    //        query = "hash = @hash"
    //    });

    //    //This should still work, even though there's no hash included in the fields. The query system is smart enough to figure it out, even though it's
    //    //a remapped field.
    //    var result = await service.SearchUnrestricted(search);

    //    //This only works when the file type is 3. Dumb tests
    //    Assert.Equal(3, (int)InternalContentType.file);

    //    Assert.NotEmpty(result.data["file"]);
    //    Assert.Equal("4", result.data["file"].First()["id"].ToString());
    //    //await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
    //    //    var result = await service.SearchUnrestricted(search);
    //    //});
    //}

    [Fact]
    public async Task GenericSearch_Search_RemappedField_FailGracefully()
    {
        var search = new SearchRequests();
        search.values.Add("revid", 1);
        search.values.Add("type", 1);
        search.requests.Add(new SearchRequest()
        {
            type = "content",
            fields = "id", 
            query = "lastRevisionId > @revid and contentType = @type"
        });

        //This fails because the field in question is complex and thus is required to be included
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

        var result = (await service.SearchUnrestricted(search)).objects;
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

        var result = (await service.Search(search)).objects["permissiondefault"];
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
            var result = (await service.Search(search, 1 + ((fixture.UserCount - 1) & ~((int)UserVariations.Super)))).objects["permission"];
            Assert.Equal(fixture.ContentCount, result.Count());
        }
        else
        {
            var uid = 1;
            var result = (await service.Search(search, uid)).objects["permission"];
            var casted = service.ToStronglyTyped<ContentView>(result);
            Assert.All(casted, x => {
                //They're not in "the group", so they are either the creator OR the permissions have them directly in it
                Assert.True(x.createUserId == uid || x.permissions.ContainsKey(uid) || x.permissions.ContainsKey(0), $"Found invalid content {x.id} for user {uid}, createuser: {x.createUserId}");
            });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1 + (int)UserVariations.Super + (int)UserVariations.Variables)]
    [InlineData(1 + (int)UserVariations.Variables)] //NOT super
    [InlineData(1 + (int)UserVariations.Special, true)] //Just some random user that doesn't have variables
    public async Task GenericSearch_Search_OnlyUserVariables(long uid, bool empty = false)
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            type = "uservariable",
            fields = "*"
        });

        var result = (await service.Search(search, uid)).objects["uservariable"];
        var castResult = service.ToStronglyTyped<UserVariable>(result);

        if(empty)
        {
            Assert.Empty(result);
            return;
        }

        Assert.Equal(uid, result.Count());

        for(var i = 0; i < castResult.Count(); i++)
        {
            Assert.Equal(uid, castResult[i].userId);
            Assert.Equal($"userval_{i}_{uid - 1}", castResult[i].key);
            Assert.Equal($"value_{i}", castResult[i].value);
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
            var result = (await service.SearchUnrestricted(search)).objects["permissionmacro"];
            Assert.Equal(fixture.ContentCount / 2, result.Count());
        }
        else
        {
            await Assert.ThrowsAnyAsync<ParseException>(async () => {
                var result = (await service.SearchUnrestricted(search)).objects["permissionmacro"];
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

            var result = (await service.SearchUnrestricted(search)).objects["ingroupmacro"];
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

        var result = (await service.SearchUnrestricted(search)).objects["keywordmacro"];
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

        var result = (await service.SearchUnrestricted(search)).objects["keywordmacro"];
        var castResult = service.ToStronglyTyped<ContentView>(result);
        //This also secretly tests if ToStronglyTyped accepts blanks
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_HasKeyword()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = "content",
            fields = "id,keywords",
            query = "!haskeyword({{one}})"
        }); //, new Dictionary<string, object> { { "search", "one" }});

        Assert.True(result.Count > 0);

        //Just make sure all the results have that keyword
        Assert.All(result, x =>
        {
            Assert.Contains("one", x.keywords);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_HasKeyword_none()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = "content",
            fields = "id,keywords",
            query = "!haskeyword({{none}})"
        }); //, new Dictionary<string, object> { { "search", "one" }});

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_AvoidKeyword()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = "content",
            fields = "id,keywords",
            query = "!avoidkeyword({{one}})"
        }); //, new Dictionary<string, object> { { "search", "one" }});

        Assert.True(result.Count > 0);

        //Just make sure all the results have that keyword
        Assert.All(result, x =>
        {
            Assert.DoesNotContain("one", x.keywords);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_KeywordCount()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = "content",
            fields = "id,keywordCount",
            query = "keywordCount > {{0}}"
        });

        Assert.True(result.Count > 0);
        Assert.All(result, x => Assert.True(x.keywordCount > 0));
    }

    [Fact]
    public async Task GenericSearch_Search_KeywordCount_Zero()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = "content",
            fields = "id,keywordCount",
            query = "keywordCount = {{0}}"
        });

        Assert.True(result.Count > 0);
        Assert.All(result, x => Assert.True(x.keywordCount == 0));
    }


    [Fact]
    public async Task GenericSearch_Search_ValueMacro()
    {
        var search = new SearchRequests();
        search.values.Add("key", "contentval_%");
        search.values.Add("search", "%value_%");
        search.requests.Add(new SearchRequest()
        {
            name = "valuemacro",
            type = "content",
            fields = "id",
            query = "!valuelike(@key, @search)"
        });

        var result = (await service.SearchUnrestricted(search)).objects["valuemacro"];
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

        var result = (await service.SearchUnrestricted(search)).objects["valuemacro"];
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenericSearch_Search_KeywordInMacro()
    {
        var values = new Dictionary<string, object> { 
            { "search", new [] { "one", "two"}}
        };
        var search = new SearchRequest()
        {
            type = "content",
            fields = "id,keywords",
            query = "!keywordin(@search)"
        };

        var result = await service.SearchSingleTypeUnrestricted<ContentView>(search, values);

        //At least half the content should have the "one" keyword, and they should all have
        //an id with the keyword flag set
        //Assert.Equal(fixture.ContentCount / 2, result.Count());
        Assert.Contains(result, x => x.keywords.Contains("one") && x.keywords.Contains("two"));
        Assert.All(result, x =>
        {
            Assert.True(x.keywords.Contains("one") || x.keywords.Contains("two"));
        });
    }

    [Fact]
    public async Task GenericSearch_Search_ValueInMacro()
    {
        var cid = (int)ContentVariations.Values + 1;
        var keys = new List<string>{ $"contentval_0_{cid}", $"contentval_1_{cid}" };
        var searches = new List<string>{ @"""value_0""", @"""value_1""" };
        var values = new Dictionary<string, object> {
            { "key", keys},
            { "search", searches}
        };
        var search = new SearchRequest()
        {
            type = "content",
            fields = "id,values",
            query = "!valuein(@key, @search)"
        };

        var result = await service.SearchSingleTypeUnrestricted<ContentView>(search, values); //(await service.SearchUnrestricted(search)).data["valuemacro"];
        //var castResult = service.ToStronglyTyped<ContentView>(result);

        //At least half the content should have the "one" keyword, and they should all have
        //an id with the keyword flag set
        //Assert.Equal(fixture.ContentCount / 2, result.Count());
        var searchesPrime = searches.Select(x => x.Trim('"')).ToList();
        Assert.Contains(result, x => x.values.ContainsKey(keys[0]) && x.values.ContainsKey(keys[1]));
        Assert.Contains(result, x => x.values.Values.Contains(searchesPrime[0]) && x.values.Values.Contains(searchesPrime[1]));
        Assert.All(result, x =>
        {
            Assert.True(x.values.ContainsKey(keys[0]) || x.values.ContainsKey(keys[1]));
            Assert.True(x.values.Values.Contains(searchesPrime[0]) || x.values.Values.Contains(searchesPrime[1]));
            //Assert.True(((x.id - 1) & (int)ContentVariations.Values) > 0);
        });
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task GenericSearch_Search_ValueKeyNotInMacro(int id)
    {
        var antiKey = $"contentval_{id}";
        var search = new SearchRequests();
        search.values.Add("key", new[] { antiKey });
        search.requests.Add(new SearchRequest()
        {
            type = "content",
            fields = "id,values",
            query = "!valuekeynotin(@key)"
        });

        var result = (await service.SearchUnrestricted(search)).objects["content"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        Assert.Contains(castResult, x => x.values.Count > 0);
        Assert.Contains(castResult, x => x.values.Count == 0);
        Assert.All(castResult, x =>
        {
            Assert.DoesNotContain(antiKey, x.values.Keys);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task GenericSearch_Search_ValueKeyNotLikeMacro(int id)
    {
        var antiKey = $"contentval_{id}";
        var search = new SearchRequests();
        search.values.Add("key", antiKey);
        search.requests.Add(new SearchRequest()
        {
            type = "content",
            fields = "id,values",
            query = "!valuekeynotlike(@key)"
        });

        var result = (await service.SearchUnrestricted(search)).objects["content"];
        var castResult = service.ToStronglyTyped<ContentView>(result);

        Assert.Contains(castResult, x => x.values.Count > 0);
        Assert.Contains(castResult, x => x.values.Count == 0);
        Assert.All(castResult, x =>
        {
            Assert.DoesNotContain(antiKey, x.values.Keys);
        });
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

        var result = (await service.SearchUnrestricted(search)).objects["parentmacro"];
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

        var result = (await service.SearchUnrestricted(search)).objects["parentmacro"];
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

        var result = (await service.SearchUnrestricted(search)).objects;
        var castResult = service.ToStronglyTyped<ActivityView>(result["historymacro"]);
        var content = service.ToStronglyTyped<ContentView>(result["actcon"]);

        Assert.All(castResult, x =>
        {
            var c = content.First(y => y.id == x.contentId);
            Assert.False(c.deleted);
            Assert.Equal(InternalContentType.page, c.contentType);
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

            var result = (await service.Search(search)).objects["watches"];
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
        search.values.Add("type", 1);
        search.requests.Add(new SearchRequest()
        {
            name = "allreadable",
            type = "content",
            fields = "*",
            query = "contentType = @type"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "allcomments",
            type = "message",
            fields = "id, text, contentId, createUserId",
            query = "contentId in @allreadable.id"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "createusers",
            type = "user",
            fields = "id, username, special, avatar",
            query = "id in @allreadable.createUserId or id in @allcomments.createUserId"
        });

        //Get results as "default" user (meaning not logged in)
        var result = (await service.Search(search, uid)).objects;

        Assert.Contains("allreadable", result.Keys);
        Assert.Contains("createusers", result.Keys);
        Assert.Contains("allcomments", result.Keys);

        var content = service.ToStronglyTyped<ContentView>(result["allreadable"]);
        var users = service.ToStronglyTyped<UserView>(result["createusers"]);
        var comments = service.ToStronglyTyped<MessageView>(result["allcomments"]);

        Assert.All(content, x => 
        {
            Assert.Equal(InternalContentType.page, x.contentType);
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
            type = "message",
            fields = "*",
            query = "id in @allreadable.lastCommentId"
        });

        //Get results as "default" user (meaning not logged in)
        var result = (await service.Search(search)).objects;

        Assert.Contains("allreadable", result.Keys);
        Assert.Contains("lastcomments", result.Keys);

        var content = service.ToStronglyTyped<ContentView>(result["allreadable"]);
        var comments = service.ToStronglyTyped<MessageView>(result["lastcomments"]);
        
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

    //This test ensures the basic structure of message_aggregate makes sense
    [Theory]
    [InlineData(0)]
    [InlineData(0, "id > @id")]
    [InlineData(0, "createDate > @past")]
    [InlineData((int)UserVariations.Super)]
    [InlineData((int)UserVariations.Super, "id > @id")]
    [InlineData((int)UserVariations.Super, "createDate > @past")]
    [InlineData((int)UserVariations.Super + 1)]
    [InlineData((int)UserVariations.Super + 1, "id > @id")]
    [InlineData((int)UserVariations.Super + 1, "createDate > @past")]
    public async Task GenericSearch_Search_CommentAggregateSimple(long userId, string query = "")
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            type = "message_aggregate",
            fields = "*",
            query = query
        });
        search.values.Add("id", 10);
        search.values.Add("past", DateTime.UtcNow - TimeSpan.FromDays(5));

        var result = (userId == 0 ? (await service.SearchUnrestricted(search)) : (await service.Search(search, userId)))
            .objects["message_aggregate"];
        var castResult = service.ToStronglyTyped<MessageAggregateView>(result);

        //There should ALWAYS be results
        Assert.True(castResult.Count > 0, "There were no results!");

        //This is a weird little thing to ensure the grouping works
        var groupSet = castResult.Select(x => $"{x.contentId}|{x.createUserId}" ).ToList();

        //Ensure that there are no duplicates in groupBy
        Assert.Equal(groupSet.Count, groupSet.Distinct().Count());

        Assert.All(castResult, x =>
        {
            Assert.True(x.minId <= x.maxId);
            Assert.True(x.minCreateDate <= x.maxCreateDate);
            Assert.True(x.contentId != 0);
            Assert.True(x.createUserId != 0);
            Assert.True(x.count > 0); //THIS IS TRUE because a groupby would not include groups that don't exist!
        });
    }

    //This test ensures the basic structure of activity_aggregate makes sense
    [Theory]
    [InlineData(0)]
    [InlineData(0, "id > @id")]
    [InlineData(0, "createDate > @past")]
    [InlineData((int)UserVariations.Super)]
    [InlineData((int)UserVariations.Super, "id > @id")]
    [InlineData((int)UserVariations.Super, "createDate > @past")]
    [InlineData((int)UserVariations.Super + 1)]
    [InlineData((int)UserVariations.Super + 1, "id > @id")]
    [InlineData((int)UserVariations.Super + 1, "createDate > @past")]
    public async Task GenericSearch_Search_ActivityAggregateSimple(long userId, string query = "")
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            type = "activity_aggregate",
            fields = "*",
            query = query
        });
        search.values.Add("id", 10);
        search.values.Add("past", DateTime.UtcNow - TimeSpan.FromDays(5));

        var result = (userId == 0 ? (await service.SearchUnrestricted(search)) : (await service.Search(search, userId)))
            .objects["activity_aggregate"];
        var castResult = service.ToStronglyTyped<ActivityAggregateView>(result);

        //There should ALWAYS be results
        Assert.True(castResult.Count > 0, "There were no results!");

        //This is a weird little thing to ensure the grouping works
        var groupSet = castResult.Select(x => $"{x.contentId}|{x.createUserId}" ).ToList();

        //Ensure that there are no duplicates in groupBy
        Assert.Equal(groupSet.Count, groupSet.Distinct().Count());

        Assert.All(castResult, x =>
        {
            Assert.True(x.minId <= x.maxId);
            Assert.True(x.minCreateDate <= x.maxCreateDate);
            Assert.True(x.contentId != 0);
            Assert.True(x.createUserId != 0);
            Assert.True(x.count > 0); //THIS IS TRUE because a groupby would not include groups that don't exist!
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData((int)UserVariations.Super)]
    [InlineData((int)UserVariations.Super + 1)]
    public async Task GenericSearch_Search_KeywordAggregateSimple(long userId, string query = "")
    {
        var search = new SearchRequests();
        search.requests.Add(new SearchRequest()
        {
            type = "keyword_aggregate",
            fields = "*",
            query = query
        });

        var result = (userId == 0 ? (await service.SearchUnrestricted(search)) : (await service.Search(search, userId)))
            .objects["keyword_aggregate"];
        var castResult = service.ToStronglyTyped<KeywordAggregateView>(result);

        //There should ALWAYS be results
        Assert.True(castResult.Count > 0, "There were no results!");

        //This is a weird little thing to ensure the grouping works
        Assert.Equal(castResult.Select(x => x.value).Distinct().Count(), castResult.Count);
        Assert.All(castResult, x =>
        {
            Assert.True(x.count > 0);
        });
    }

    [Fact]
    public async Task Regression_MessageAggregatePrivacy()
    {
        var results = await service.SearchSingleType<MessageAggregateView>(NormalUserId, new SearchRequest()
        {
            type = "message_aggregate",
            fields = "*"
        });

        Assert.DoesNotContain(results, x => x.contentId == SuperAccessContentId);
        Assert.Contains(results, x => x.contentId == AllAccessContentId);
    }

    [Fact]
    public async Task Regression_ActivityAggregatePrivacy()
    {
        var results = await service.SearchSingleType<ActivityAggregateView>(NormalUserId, new SearchRequest()
        {
            type = "activity_aggregate",
            fields = "*"
        });

        Assert.DoesNotContain(results, x => x.contentId == SuperAccessContentId);
        Assert.Contains(results, x => x.contentId == AllAccessContentId);
    }

    //This test may seem superfluous, but it's important to me, because I need to ensure that
    //comments and module messages make sense together and don't overlap, and that the system
    //(which includes weird stuff like receiveUserId filtering) isn't doing something nuts
    [Fact]
    public async Task SearchUnrestricted_ModulesAndComments()
    {
        //We want to get both comments and module messages at once
        var all = await service.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest()
        {
            type = "message",
            fields = "*"
        });

        //And then get module messages and comments separately
        var comments = await service.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = "!null(module)"
        });

        var moduleMessages = await service.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = "!notnull(module)"
        });

        var allids = all.Select(x => x.id);
        var commentids = comments.Select(x => x.id);
        var modulemids = moduleMessages.Select(x => x.id);
        Assert.Equal(all.Count, comments.Count + moduleMessages.Count);
        Assert.Empty(commentids.Intersect(modulemids));
        Assert.True(allids.Intersect(commentids).OrderBy(x => x).SequenceEqual(commentids.OrderBy(x => x)));
        Assert.True(allids.Intersect(modulemids).OrderBy(x => x).SequenceEqual(modulemids.OrderBy(x => x)));
    }

    [Theory]
    [InlineData((int)UserVariations.Super)]
    [InlineData(1 + (int)UserVariations.Super)]
    [InlineData(0)]
    public async Task Search_ReceiveUserIds(long uid)
    {
        var searchreq = new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = $"contentId IN @cids"
        };
        var values = new Dictionary<string, object> {
            { "cids", new [] { 
                1 + (int)ContentVariations.AccessByAll + (int)ContentVariations.Comments,
                1 + (int)ContentVariations.AccessByAll + (int)ContentVariations.Keywords, //jUST ANY high non-comments variation will be mod messages
            }} 
        };
        //NOTE: limitation of receiveUserId for comments is enforced by the controller. The writer
        //doesn't care, you can write comments with a receiveUserid and they will function like
        //private messages.
        var all = await service.SearchSingleTypeUnrestricted<MessageView>(searchreq, values);
        var desiredMessages = all.Where(x => x.receiveUserId == 0 || x.receiveUserId == uid).ToList();

        //Make sure the test itself makes sense
        Assert.Contains(all, x => x.module != null); //Modules
        Assert.Contains(all, x => x.module == null); //Non modules
        Assert.Contains(all, x => x.receiveUserId == 0); //Broadcast
        Assert.Contains(all, x => x.receiveUserId != 0); //Not broadcast
        Assert.Contains(all, x => x.receiveUserId == uid); //Something for us
        Assert.Contains(all, x => x.receiveUserId != 0 && x.receiveUserId != uid); //A private message NOT for us

        Assert.NotEqual(all.Count, desiredMessages.Count);

        var searchMessages = await service.SearchSingleType<MessageView>(uid, searchreq, values);

        //Regardless of who's searching, I KNOW we don't have all the messages!!
        Assert.NotEqual(all.Count, searchMessages.Count);
        Assert.NotEqual(1000, searchMessages.Count); // I also want to make sure we're not auto-limited
        Assert.Equal(desiredMessages.Count, searchMessages.Count);

        //And finally, just make sure they were found exactly
        Assert.True(desiredMessages.Select(x => x.id).OrderBy(x => x).SequenceEqual(searchMessages.Select(x => x.id).OrderBy(x => x)));
    }

    [Theory]
    [InlineData(NormalUserId)]
    [InlineData(SuperUserId)]
    public async Task SearchAsync_ContentEngagementOnlyForSelf(long uid)
    {
        //go try to get all the votes
        var votes = await service.SearchSingleType<ContentEngagementView>(uid, new SearchRequest()
        {
            type = nameof(RequestType.content_engagement),
            fields = "*"
        });

        Assert.NotEmpty(votes);

        //Ensure they're ALL just for us, nobody else
        Assert.All(votes, x => 
        {
            Assert.Equal(uid, x.userId);
            Assert.True(x.contentId > 0);
            Assert.False(string.IsNullOrEmpty(x.engagement));
            Assert.NotEqual("none", x.engagement);
        });
    }

    [Theory]
    [InlineData(NormalUserId)]
    [InlineData(SuperUserId)]
    public async Task SearchAsync_MessageEngagementOnlyForSelf(long uid)
    {
        //go try to get all the votes
        var votes = await service.SearchSingleType<MessageEngagement>(uid, new SearchRequest()
        {
            type = nameof(RequestType.message_engagement),
            fields = "*"
        });

        Assert.NotEmpty(votes);

        //Ensure they're ALL just for us, nobody else
        Assert.All(votes, x => 
        {
            Assert.Equal(uid, x.userId);
            Assert.True(x.messageId > 0);
            Assert.False(string.IsNullOrEmpty(x.engagement));
            Assert.NotEqual("none", x.engagement);
        });
    }

    [Fact]
    public async Task SearchAsync_ReusableSearchAndValues()
    {
        var search = new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = "id > @minid",
            limit = 10
        };
        var values = new Dictionary<string, object> {
            { "minid", 5 }
        };

        //You should be able to reuse the search and values
        var messages = await service.SearchSingleType<MessageView>(NormalUserId, search, values);
        var messages2 = await service.SearchSingleType<MessageView>(NormalUserId, search, values);

        Assert.NotEmpty(messages);
        Assert.NotEmpty(messages2);
    }

    [Fact]
    public async Task SearchAsyncUnrestricted_ReusableSearchAndValues()
    {
        var search = new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = "id > @minid",
            limit = 10
        };
        var values = new Dictionary<string, object> {
            { "minid", 5 }
        };

        //You should be able to reuse the search and values
        var messages = await service.SearchSingleTypeUnrestricted<MessageView>(search, values);
        var messages2 = await service.SearchSingleTypeUnrestricted<MessageView>(search, values);

        Assert.NotEmpty(messages);
        Assert.NotEmpty(messages2);
    }

    [Fact]
    public async Task SearchAsync_AnyBans()
    {
        var search = new SearchRequest()
        {
            type = "ban",
            fields = "*",
            query = ""
        };
        var values = new Dictionary<string, object> { };

        //You should be able to reuse the search and values
        var bans = await service.SearchSingleType<BanView>(NormalUserId, search, values);

        Assert.NotEmpty(bans);
    }

    [Fact]
    public async Task SearchAsync_ActiveBansMacro()
    {
        var search = new SearchRequest()
        {
            type = "ban",
            fields = "*",
            query = "!activebans()"
        };
        var values = new Dictionary<string, object> { };

        //You should be able to reuse the search and values
        var bans = await service.SearchSingleType<BanView>(NormalUserId, search, values);

        Assert.NotEmpty(bans);
    }

    /*[Theory]
    [InlineData(NormalUserId)]
    [InlineData(SuperUserId)]
    public async Task SearchAsync_UserpageMacro(long userId)
    {
        var search = new SearchRequest()
        {
            type = "content",
            fields = "*",
            query = "!userpage(@uid)"
        };
        var values = new Dictionary<string, object> { { "uid", userId } };

        //You should be able to reuse the search and values
        var userpages = await service.SearchSingleTypeUnrestricted<ContentView>(search, values);

        Assert.NotEmpty(userpages);
        Assert.All(userpages, x => {
            Assert.Equal(InternalContentType.userpage, x.contentType);
        });
    }*/

    [Fact]
    public async Task SearchAsync_UserpageMacro()
    {
        var search = new SearchRequest()
        {
            type = "content",
            fields = "id, createUserId, contentType",
            query = "contentType = @userpage and !notdeleted()"
        };
        var values = new Dictionary<string, object> { { "userpage", InternalContentType.userpage } };

        //You should be able to reuse the search and values
        var userpages = await service.SearchSingleTypeUnrestricted<ContentView>(search, values);

        //Now go try to lookup all the junk, there should be one userpage for each createuser in the userpage list
        foreach(var up in userpages)
        {
            search = new SearchRequest()
            {
                type = "content",
                fields = "id, createUserId, contentType",
                query = "!userpage(@uid)"
            };
            values = new Dictionary<string, object> { { "uid", up.createUserId } };
            var thisuserpages = await service.SearchSingleTypeUnrestricted<ContentView>(search, values);
            Assert.NotEmpty(thisuserpages);
            Assert.All(thisuserpages, x => {
                Assert.Equal(InternalContentType.userpage, x.contentType);
                Assert.Equal(up.createUserId, x.createUserId);
            });
        }
    }

    [Fact]
    public async Task SearchAsync_BasicCommentMacro()
    {
        var search = new SearchRequest()
        {
            type = "message",
            fields = "id, createUserId, deleted, module",
            query = "!basiccomments()"
        };
        //var values = new Dictionary<string, object> { { "userpage", InternalContentType.userpage } };

        //You should be able to reuse the search and values
        var comments = await service.SearchSingleTypeUnrestricted<MessageView>(search); //, values);

        Assert.NotEmpty(comments);
        Assert.All(comments, (comment) =>
        {
            Assert.False(comment.deleted);
            Assert.Null(comment.module);
        });
        //comments.

        //Now go try to lookup all the junk, there should be one userpage for each createuser in the userpage list
        //foreach(var up in userpages)
        //{
        //    search = new SearchRequest()
        //    {
        //        type = "content",
        //        fields = "id, createUserId, contentType",
        //        query = "!userpage(@uid)"
        //    };
        //    values = new Dictionary<string, object> { { "uid", up.createUserId } };
        //    var thisuserpages = await service.SearchSingleTypeUnrestricted<ContentView>(search, values);
        //    Assert.NotEmpty(thisuserpages);
        //    Assert.All(thisuserpages, x => {
        //        Assert.Equal(InternalContentType.userpage, x.contentType);
        //        Assert.Equal(up.createUserId, x.createUserId);
        //    });
        //}
    }

    protected SearchRequests GetCountSearch(string type, string query = "", List<string>? extraFields = null)
    {
        var realFields = extraFields ?? new List<string>();
        realFields.Add("id");
        realFields.Add(Constants.CountField);

        return new SearchRequests()
        {
            requests = new List<SearchRequest> {
                new SearchRequest()
                {
                    type = type,
                    fields = string.Join(",", realFields),
                    query = query
                }
            }
        };
    }

    protected async Task<long>SearchSimpleCount(SearchRequests request, long userId = NormalUserId)
    {
        var result = userId < 0 ? await service.SearchUnrestricted(request) : await service.Search(request, userId);

        var type = request.requests.First().type;
        Assert.Contains(type, result.objects.Keys);
        Assert.Single(result.objects[type]);
        Assert.Contains(Constants.CountField, result.objects[type].First());
        return (long)result.objects[type].First()[Constants.CountField];
    }

    protected Task<long> SearchSimpleCount(string type, long userId = NormalUserId, string query = "", List<string>? extraFields = null)
    {
        var search = GetCountSearch(type, query, extraFields);
        return SearchSimpleCount(search, userId);
    }

    [Fact]
    public async Task SearchAsync_CountUsers()
    {
        var count = await SearchSimpleCount(nameof(RequestType.user));
        Assert.Equal(fixture.UserCount + fixture.GroupCount, count);
    }

    [Fact]
    public async Task SearchAsync_CountContent()
    {
        var count = await SearchSimpleCount(nameof(RequestType.content), NormalUserId);
        Assert.Equal(fixture.ContentCount / 2, count);
    }

    [Fact]
    public async Task SearchAsync_CountContentUnrestricted()
    {
        var count = await SearchSimpleCount(nameof(RequestType.content), -1);
        Assert.Equal(fixture.ContentCount, count);
    }

    [Fact]
    public async Task SearchAsync_CountMessages()
    {
        var messages = await service.SearchSingleType<Message>(NormalUserId, new SearchRequest()
        {
            type = nameof(RequestType.message),
            fields = "id, contentId"
        });

        var count = await SearchSimpleCount(nameof(RequestType.message), NormalUserId, "", new List<string> { "contentId" });
        Assert.Equal(messages.Count, count);
    }

    [Fact]
    public async Task SearchAsync_CountMessagesUnrestricted()
    {
        var messages = await service.SearchSingleTypeUnrestricted<Message>(new SearchRequest()
        {
            type = nameof(RequestType.message),
            fields = "id"
        });

        var count = await SearchSimpleCount(nameof(RequestType.message), -1);
        Assert.Equal(messages.Count, count);
    }

    [Fact]
    public async Task SearchAsync_CountMessages_Query1()
    {
        var query = "contentId > @minId";
        var values = new Dictionary<string, object> {
            { "minId", 23 }
        };

        var messages = await service.SearchSingleType<Message>(NormalUserId, new SearchRequest()
        {
            type = nameof(RequestType.message),
            query = query,
            fields = $"id, contentId"
        }, values);

        var countSearch = GetCountSearch(nameof(RequestType.message), query, new List<string> { "contentId"} );
        countSearch.values = values;

        var count = await SearchSimpleCount(countSearch, NormalUserId);
        Assert.Equal(messages.Count, count);
    }

    [Fact]
    public async Task Regression_SearchAsync_DuplicateValues()
    {
        var search = new SearchRequests()
        {
        	values = new Dictionary<string, object> { { "pid", AllAccessContentId } },
            requests = new List<SearchRequest> {
                new SearchRequest { type = "content",fields = "*", query="id = @pid"},
                new SearchRequest {type="watch", fields="*", query= "contentId IN @content.id"}, //This is fine
                new SearchRequest {type="message", fields="*", query= "contentId IN @content.id", order= "id_desc", limit= 30}, //This broke
            }
        };

        var result = await service.Search(search, SuperUserId);

        Assert.NotEmpty(result.objects["content"]);
        Assert.NotEmpty(result.objects["message"]);
        //Assert.NotEmpty(result.objects["watch"]);
    }

    [Fact]
    public async Task SearchAsync_QueryBuilder_LiteralSugar_Single()
    {
        var result = await service.SearchSingleType<ContentView>(SuperUserId, new SearchRequest()
        {
            type = nameof(RequestType.content),
            fields = "*",
            query = "id = {{"+AllAccessContentId+"}}"
        });

        Assert.Single(result);
        Assert.Contains(result, x => x.id == AllAccessContentId);
    }

    [Fact]
    public async Task SearchAsync_QueryBuilder_LiteralSugar_Single_Unrestricted()
    {
        var result = await service.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest()
        {
            type = nameof(RequestType.content),
            fields = "*",
            query = "id = {{" + AllAccessContentId + "}}"
        });

        Assert.Single(result);
        Assert.Contains(result, x => x.id == AllAccessContentId);
    }

    [Fact]
    public async Task SearchAsync_QueryBuilder_LiteralSugar_Double()
    {
        var result = await service.SearchSingleType<ContentView>(SuperUserId, new SearchRequest()
        {
            type = nameof(RequestType.content),
            fields = "*",
            query = "id = {{" + AllAccessContentId + "}} or id = {{" + SuperAccessContentId + "}}"
        });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.id == AllAccessContentId);
        Assert.Contains(result, x => x.id == SuperAccessContentId);
    }

    [Fact]
    public async Task Regression_SearchAsync_ZeroSpecialCount()
    {
        var result = await service.Search(new SearchRequests() {
            requests = new List<SearchRequest>() {
                new SearchRequest() {
                    type = nameof(RequestType.content),
                    fields = $"{nameof(ContentView.id)},{Constants.CountField}",
                    query = "id > {{9999}}"
                }
            }
        }, NormalUserId);

        Assert.Contains(nameof(RequestType.content), result.objects.Keys);
        Assert.Equal(0, (long)result.objects[nameof(RequestType.content)].First()[Constants.CountField]);
    }
}