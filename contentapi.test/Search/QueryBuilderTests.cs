using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using contentapi.Search;
using contentapi.data;
using contentapi.data.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

//WARN: ALL TESTS THAT ACCESS THE SEARCHFIXTURE SHOULD GO IN HERE! Otherwise the database
//will be created for EVERY class that uses the fixture, increasing the test time! Just
//keep it together, even if the class gets large!
public class QueryBuilderTests : UnitTestBase
{
    protected QueryBuilder service;
    protected IViewTypeInfoService typeInfoService;

    public QueryBuilderTests()
    {
        service = new QueryBuilder(GetService<ILogger<QueryBuilder>>(), 
            GetService<IViewTypeInfoService>(), GetService<IMapper>(), GetService<ISearchQueryParser>(),
            GetService<IPermissionService>());
        typeInfoService = GetService<IViewTypeInfoService>();
    }

    [Fact]
    public void StandardRequestPreparse_Normal()
    {
        var result = service.StandardRequestPreparse(new SearchRequest()
        {
            name = "somename",
            type = "user",
            fields = "*"
        }, new Dictionary<string, object>());

        Assert.Equal("somename", result.name);
        Assert.Equal("user", result.type);
        Assert.Equal(RequestType.user, result.requestType);
        Assert.NotNull(result.typeInfo);
        Assert.Equal("users", result.typeInfo.selectFromSql);
        Assert.NotEmpty(result.requestFields);
    }

    [Fact]
    public void ComputeRealFields_NoChange()
    {
        var req = new SearchRequestPlus() {
            fields = "id, username",
            typeInfo = typeInfoService.GetTypeInfo<UserView>()
        };

        var fields = service.ComputeRealFields(req);
        Assert.True(fields.SequenceEqual(new [] { "id", "username" }), "Simple fields were not preserved in ComputeRealFields!");
    }

    [Fact]
    public void ComputeRealFields_Star()
    {
        var req = new SearchRequestPlus() {
            fields = "*",
            typeInfo = typeInfoService.GetTypeInfo<UserView>()
        };

        var fields = service.ComputeRealFields(req);
        Assert.True(new HashSet<string>(req.typeInfo.fields.Keys).SetEquals(fields), "Star didn't generate all queryable fields in ComputeRealFields!");
    }

    [Fact]
    public void ComputeRealFields_Inverted()
    {
        var req = new SearchRequestPlus() {
            fields = "~ id, username", //This also makes sure spaces are trimmed
            typeInfo = typeInfoService.GetTypeInfo<UserView>()
        };

        var fields = service.ComputeRealFields(req);
        var realSet = req.typeInfo.fields.Keys.Except(new[] {"id","username"});
        Assert.True(new HashSet<string>(realSet).SetEquals(fields), "Inverted didn't generate correct set in ComputeRealFields!");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(" ")]
    public void StandardRequestPreparse_NoEmptyFields(string fields)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var result = service.StandardRequestPreparse(new SearchRequest()
            {
                name = "somename",
                type = "user",
                fields = fields
            }, new Dictionary<string, object>());
        });
    }

    [Fact]
    public void GetAboutSearch_SomethingReturned()
    {
        var result = service.GetAboutSearch();
        Assert.NotEmpty(result.macros);
        Assert.NotEmpty(result.types);
        Assert.NotEmpty(result.objects);
    }

    [Fact]
    public void FullParseRequest_User()
    {
        var result = service.FullParseRequest(new SearchRequest()
        {
            name = "somename",
            type = "user",
            fields = "*"
        }, new Dictionary<string, object>());

        Assert.Equal("somename", result.name);
        Assert.Equal("user", result.type);
        Assert.Equal(RequestType.user, result.requestType);
        Assert.NotNull(result.typeInfo);
        Assert.Equal("users", result.typeInfo.selectFromSql);
        Assert.NotEmpty(result.requestFields);

        //Even if WE can't parse it, the field BETTER show up in request fields! This is the only
        //way to allow the searcher to add additional data!
        Assert.Contains("groups", result.requestFields);

        //And because groups are not part of the query builder, it should NOT show up in the query
        Assert.DoesNotContain("groups", result.computedSql);
    }

    [Theory]
    [InlineData("*", true)]
    [InlineData("id", true)]
    [InlineData("name", true)]
    [InlineData("createDate,createUserId", true)]
    [InlineData("id,values", true)]
    [InlineData("values", false)]
    [InlineData("keywords,permissions", false)]
    [InlineData("id,values,keywords,permissions", true)]
    public void FullParseRequest_AllowedFieldSets(string fields, bool allowed)
    {
        var request =new SearchRequest()
        {
            name = "contentTest",
            type = "content",
            fields = fields
        };

        if(allowed)
        {
            var result = service.FullParseRequest(request, new Dictionary<string, object>());
            Assert.NotEmpty(result.computedSql);
        }
        else
        {
            try
            {
                var result = service.FullParseRequest(request, new Dictionary<string, object>());
                Assert.False(true, "FullParseRequest should've thrown an exception but did not!");
            }
            catch(ArgumentException)
            {
                //This is expected
            }
        }
    }

    [Theory]
    [InlineData("*", "id", true)]
    [InlineData("*", "id_desc", true)]
    [InlineData("*", "createUserId", true)]
    [InlineData("*", "createUserId_desc", true)]
    [InlineData("*", "id,createUserId", true)]
    [InlineData("*", "id_desc,createUserId", true)]
    [InlineData("*", "id_desc,createUserId_desc", true)]
    [InlineData("*", "id,createUserId_desc", true)]
    [InlineData("id", "createUserId", false)]
    public void FullParseRequest_AllowedOrders(string fields, string order, bool allowed)
    {
        var request =new SearchRequest()
        {
            name = "contentTest",
            type = "content",
            fields = fields,
            order = order
        };

        if(allowed)
        {
            var result = service.FullParseRequest(request, new Dictionary<string, object>());
            Assert.NotEmpty(result.computedSql);
        }
        else
        {
            try
            {
                var result = service.FullParseRequest(request, new Dictionary<string, object>());
                Assert.False(true, "FullParseRequest should've thrown an exception but did not!");
            }
            catch(ArgumentException)
            {
                //This is expected
            }
        }
    }

    [Theory]
    [InlineData("id = {{123}}", "123", true)]
    [InlineData("name = {{abc}}", "abc", true)]
    [InlineData("name = {{abc}} or id = {{123}}", "123", true)]
    [InlineData("name = {{\"holy heck\"}}", "\"holy heck\"", true)]
    [InlineData("name = {{%wow%}}", "%wow", true)]
    [InlineData("name = {{something with spaces}}", "something with spaces", true)]
    [InlineData("name = {nope}", null, false)]
    public void FullParseRequest_LiteralSugar(string query, object value, bool allowed)
    {
        var request =new SearchRequest()
        {
            name = "contentTest",
            type = "content",
            fields = "*",
            query = query
        };

        var values = new Dictionary<string, object>();
        var result = new SearchRequestPlus();
        var work = new Action(() => result = service.FullParseRequest(request, values));

        if(allowed)
        {
            work();
            Assert.NotEmpty(result.computedSql);
            Assert.NotEmpty(values);
            Assert.Contains(value, values.Values);
        }
        else
        {
            Assert.ThrowsAny<ArgumentException>(work);
        }
    }
}