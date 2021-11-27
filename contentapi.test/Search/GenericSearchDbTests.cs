using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class GenericSearchDbTests : UnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected IDbConnection dbcon;
    protected GenericSearcher service;
    protected DbUnitTestSearchFixture fixture;

    public GenericSearchDbTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        var conWrap = fixture.GetService<ContentApiDbConnection>();
        service = new GenericSearcher(fixture.GetService<ILogger<GenericSearcher>>(), 
            conWrap, fixture.GetService<ITypeInfoService>(), fixture.GetService<GenericSearcherConfig>(),
            fixture.GetService<IMapper>(), fixture.GetService<ISearchQueryParser>());
        dbcon = conWrap.Connection;
    }

    [Fact]
    public void GenericSearch_ConnectionSuccessful()
    {
        //If THIS fails, it'll be because you don't have the services or database set up 
        //correctly, and thus that needs to be fixed before any other tests in here are looked at
        Assert.NotNull(dbcon);
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

            var result = service.Search(search).Result["testStar"];
            Assert.NotEmpty(result);

            //Here, we're just making sure that "*" didn't break anything. We assume
            //that "*" is implemented generically, and thus we can do some other test
            //some other time for whether all fields are returned, but that is not 
            //necessary for this broad test
        }
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

        var result = service.Search(search).Result;

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
            type = "user",
            fields = "id",
            query = "id < @maxid" //Don't need to test syntax btw! Already done!
        });

        var result = service.Search(search).Result;

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
            type = "user",
            fields = "id",
            query = "id <= @maxid"
        });

        var result = service.Search(search).Result;

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
            type = "user",
            fields = "id",
            query = "id > @minid" //Don't need to test syntax btw! Already done!
        });

        var result = service.Search(search).Result;

        Assert.Equal(fixture.UserCount - 10, result["greaterthan"].Count());
    }

    [Fact]
    public void GenericSearch_Search_GreaterThanEqual()
    {
        var search = new SearchRequests();
        search.values.Add("minid", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "greaterthanequal",
            type = "user",
            fields = "id",
            query = "id >= @minid" //Don't need to test syntax btw! Already done!
        });

        var result = service.Search(search).Result;

        Assert.Equal(fixture.UserCount - 9, result["greaterthanequal"].Count());
    }

    [Fact]
    public void GenericSearch_Search_Equal()
    {
        var search = new SearchRequests();
        search.values.Add("id", 10);
        search.requests.Add(new SearchRequest()
        {
            name = "equal",
            type = "user",
            fields = "id",
            query = "id = @id"
        });

        var result = service.Search(search).Result;

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
            type = "user",
            fields = "id",
            query = "id <> @id"
        });

        var result = service.Search(search).Result;

        Assert.Equal(fixture.UserCount - 1, result["noequal"].Count());
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
        search.values.Add("userlike", "user_1%");
        search.requests.Add(new SearchRequest()
        {
            name = "testlike",
            type = "user",
            fields = "id, username",
            query = "username like @userlike"
        });

        var result = service.Search(search).Result["testlike"];
        Assert.Equal(16, fixture.UserCount); //This test only works while this is 16
        Assert.Equal(7, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
        //Assert.Single(result);
        //var user = result.First();
        //Assert.Equal("admin", user["username"]);
        //Assert.Equal(1L, user["avatar"]);
        //Assert.Equal("cutenickname", user["special"]);
    }

    [Fact]
    public void GenericSearch_Search_NotLike()
    {
        var search = new SearchRequests();
        search.values.Add("usernotlike", "user_1%");
        search.requests.Add(new SearchRequest()
        {
            name = "testnotlike",
            type = "user",
            fields = "id",
            query = "username not like @usernotlike"
        });

        var result = service.Search(search).Result["testnotlike"];
        Assert.Equal(16, fixture.UserCount); //This test only works while this is 16
        Assert.Equal(fixture.UserCount - 7, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
    }

    [Fact]
    public void GenericSearch_Search_In()
    {
        var search = new SearchRequests();
        search.values.Add("ids", new int[] { 1, 10, 15 });
        search.requests.Add(new SearchRequest()
        {
            name = "idin",
            type = "user",
            fields = "id",
            query = "id in @ids"
        });

        var result = service.Search(search).Result["idin"];
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
            type = "user",
            fields = "id",
            query = "id not in @ids"
        });

        var result = service.Search(search).Result["idnotin"];
        Assert.Equal(fixture.UserCount - 3, result.Count()); //There are 16 users, so 6 from 10s and 1 from the 1
        Assert.DoesNotContain(1L, result.Select(x => x["id"]));
        Assert.DoesNotContain(10L, result.Select(x => x["id"]));
        Assert.DoesNotContain(15L, result.Select(x => x["id"]));
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
            var result = await service.Search(search);
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
            var result = await service.Search(search);
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

        var result = await service.Search(search);
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

        var result = (await service.Search(search))["basicfield"];
        Assert.Equal(fixture.UserCount, result.Count());
    }

    [Fact]
    public async Task GenericSearch_Search_ComplexFieldRequired_FailGracefully()
    {
        var search = new SearchRequests();
        search.values.Add("bucket", "one");
        search.requests.Add(new SearchRequest()
        {
            name = "nocomplex",
            type = "file",
            fields = "id", //Only querying id, but asking for bucket, which we know is searchable
            query = "bucket = @bucket"
        });

        await Assert.ThrowsAnyAsync<ArgumentException>(async () => {
            var result = await service.Search(search);
        });
    }

    [Fact]
    public async Task GenericSearch_Search_RemappedField_Searchable()
    {
        //This test relies on the amount of content types. If it changes, just fix it, it's easy
        Assert.Equal(4, Enum.GetValues<InternalContentType>().Count());

        var search = new SearchRequests();
        search.values.Add("bucket", fixture.StandardPublicTypes[(int)InternalContentType.file]);
        search.requests.Add(new SearchRequest()
        {
            name = "complex",
            type = "file",
            fields = "id, bucket", //Only querying id, but asking for bucket, which we know is searchable but remapped from publicType
            query = "bucket = @bucket"
        });

        var result = (await service.Search(search))["complex"];
        Assert.Equal(fixture.ContentCount / 4 / 2, result.Count());
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
                type = "user",
                fields = "id",
                query = $"id not in @{k}ids"
            });
        }
        //Here, I'm using a keyword as the start of different names. This would normally fail
        //in the regular lexer, but with the additions, it will allow this to work(?)

        var result = await service.Search(search);
        foreach(var k in keywords)
        {
            var r = result[$"{k}test"];
            Assert.Equal(fixture.UserCount - 3, r.Count());
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

        var result = (await service.SearchRestricted(search))["permissiondefault"];
        //var castedResult = ;

        //Because the permission thing "all or none" is just based on a bit, it will
        //always be HALF of the content that we're allowed to get. The ID should also
        //be related to the ones that have it.
        Assert.Equal(fixture.ContentCount / 2, result.Count());
        Assert.All(result, x =>
        {
            //Minus 1 because the database ids start at 1
            Assert.True(((((long)x["id"]) - 1) & (int)ContentVariations.AccessByAll) > 0);
            //Assert.Contains(x["permissions"].Keys)
        });
    }

    ////This test tests a LOT of systems all at once! Does the macro system work?
    ////Does the search system automatically limit, and does it do it correctly?
    ////Can we actually retrieve the last post ID for all content while doing
    ////all this other stuff?? This is the MOST like a regular user search! If this
    ////is working correctly, chances are the whole system is at least MOSTLY working
    //[Fact]
    //public void GenericSearch_SearchRestricted()
    //{
    //    var search = new SearchRequests();
    //    search.requests.Add(new SearchRequest()
    //    {
    //        name = "allreadable",
    //        type = "page",
    //        fields = "id, name, createUserId, createDate, lastPostId"
    //    });
    //    search.requests.Add(new SearchRequest()
    //    {
    //        name = "createusers",
    //        type = "user",
    //        fields = "id, username, special, avatar",
    //        query = "id in @allreadable.createUserId"
    //    });
    //    search.requests.Add(new SearchRequest()
    //    {
    //        name = "allcomments",
    //        type = "comment",
    //        fields = "id, text, contentId",
    //        query = "contentId in @allreadable.id"
    //    });

    //    //Get results as the admin user! They don't have special read permissions for everything (that's
    //    //not how supers work), but they have some private pages others can't read!
    //    var result = service.SearchRestricted(search, DbUnitTestSearchFixture.adminUser).Result;

    //    Assert.Contains("allreadable", result.Keys);
    //    Assert.Contains("createusers", result.Keys);
    //    Assert.Contains("allcomments", result.Keys);
    //    //Assert.Equal(2, result["recentpages"].Count());
    //    //Assert.Equal(2, result["createusers"].Count());
    //    //Assert.Single(result["createusers"].Where(x => 
    //    //    x["id"].Equals(1L) && x["username"].Equals("firstUser")));
    //    //Assert.Single(result["createusers"].Where(x => 
    //    //    x["id"].Equals(2L) && x["username"].Equals("admin")));
    //}
}