using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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

            if(result is IEnumerable)
                Assert.NotEmpty((IEnumerable<object>)result);
        }
    }
}