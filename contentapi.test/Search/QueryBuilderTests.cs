using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

//WARN: ALL TESTS THAT ACCESS THE SEARCHFIXTURE SHOULD GO IN HERE! Otherwise the database
//will be created for EVERY class that uses the fixture, increasing the test time! Just
//keep it together, even if the class gets large!
public class QueryBuilderTests : UnitTestBase
{
    protected QueryBuilder service;
    protected ITypeInfoService typeInfoService;

    public QueryBuilderTests()
    {
        service = new QueryBuilder(GetService<ILogger<QueryBuilder>>(), 
            GetService<ITypeInfoService>(), GetService<IMapper>(), GetService<ISearchQueryParser>());
        typeInfoService = GetService<ITypeInfoService>();
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
        Assert.Equal("users", result.typeInfo.table);
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
        Assert.True(new HashSet<string>(req.typeInfo.queryableFields).SetEquals(fields), "Star didn't generate all queryable fields in ComputeRealFields!");
    }

    [Fact]
    public void ComputeRealFields_Inverted()
    {
        var req = new SearchRequestPlus() {
            fields = "~ id, username", //This also makes sure spaces are trimmed
            typeInfo = typeInfoService.GetTypeInfo<UserView>()
        };

        var fields = service.ComputeRealFields(req);
        var realSet = req.typeInfo.queryableFields.Except(new[] {"id","username"});
        Assert.True(new HashSet<string>(realSet).SetEquals(fields), "Inverted didn't generate correct set in ComputeRealFields!");
    }
}