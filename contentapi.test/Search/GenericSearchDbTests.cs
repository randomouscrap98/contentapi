using System;
using System.Data;
using System.Linq;
using AutoMapper;
using contentapi.Db;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class GenericSearchDbTests : UnitTestBase, IClassFixture<DbUnitTestFixture>
{
    protected IDbConnection dbcon;
    protected GenericSearcher service;
    protected DbUnitTestFixture fixture;

    public GenericSearchDbTests(DbUnitTestFixture fixture)
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
            //if(result is IEnumerable)
        }
    }

    [Fact]
    public void GenericSearch_Search_SimpleValue()
    {
        var search = new SearchRequests();
        search.values.Add("userlike", "admin%");
        search.requests.Add(new SearchRequest()
        {
            name = "testValue",
            type = "user",
            fields = "id, username, special, avatar",
            query = "username like @userlike" //Don't need to test syntax btw! Already done!
        });

        //var result = (IEnumerable<object>)service.Search(search).Result["testValue"];
        //Assert.Single(result);
        //var user = (IDictionary<string, object>)result.First();
        var result = service.Search(search).Result["testValue"];
        Assert.Single(result);
        var user = result.First();
        Assert.Equal("admin", user["username"]);
        Assert.Equal(1L, user["avatar"]);
        Assert.Equal("cutenickname", user["special"]);
    }


    [Fact]
    public void GenericSearch_Search_SimpleLink()
    {
        var search = new SearchRequests();
        search.values.Add("pagedate", DateTime.Now.AddDays(-20).ToString());
        search.requests.Add(new SearchRequest()
        {
            name = "recentpages",
            type = "page",
            fields = "id, name, createUserId, createDate",
            query = "createDate > @pagedate"
        });
        search.requests.Add(new SearchRequest()
        {
            name = "createusers",
            type = "user",
            fields = "id, username, special, avatar",
            query = "id in @recentpages.createUserId"
        });

        var result = service.Search(search).Result;
        Assert.Contains("recentpages", result.Keys);
        Assert.Contains("createusers", result.Keys);
        Assert.Equal(2, result["recentpages"].Count());
        Assert.Equal(2, result["createusers"].Count());
        Assert.Single(result["createusers"].Where(x => 
            x["id"].Equals(1L) && x["username"].Equals("firstUser")));
        Assert.Single(result["createusers"].Where(x => 
            x["id"].Equals(2L) && x["username"].Equals("admin")));
        //Assert.Equal("admin", user["username"]);
        //Assert.Equal(1L, user["avatar"]);
        //Assert.Equal("cutenickname", user["special"]);
    }
}