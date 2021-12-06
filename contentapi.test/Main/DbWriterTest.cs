using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class DbWriterTest : UnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;

    public DbWriterTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), fixture.GetService<IGenericSearch>(),
            fixture.GetService<Db.ContentApiDbConnection>(), fixture.GetService<ITypeInfoService>(), fixture.GetService<IMapper>(),
            fixture.GetService<Db.History.IContentHistoryConverter>());
        searcher = fixture.GetService<IGenericSearch>();

        //Reset it for every test
        fixture.ResetDatabase();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task WriteAsync_MustSetUser(long uid)
    {
        var content = new PageView {
            createUserId = uid
        };

        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
            await writer.WriteAsync(content, uid);
        });
    }

    [Theory]
    [InlineData((int)UserVariations.Super)]
    [InlineData(1 + (int)UserVariations.Super)] //THIS one is super
    public async Task WriteAsync_CantWriteRaw(long uid)
    {
        var content = new ContentView {
            name = "Yeah",
            createDate = DateTime.Now,
            parentId = 0,
            createUserId = uid
        };

        //Don't care what type, but it should fail somehow...
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            await writer.WriteAsync(content, uid);
        });
    }

    [Theory]
    [InlineData((int)UserVariations.Super)]
    [InlineData(1 + (int)UserVariations.Super)] //THIS one is super
    public async Task WriteAsync_MostSimple(long uid)
    {
        var content = new PageView {
            name = "whatever",
            content = "Yeah this is content!",
            createDate = DateTime.Now, 
            parentId = 0, //Anyone should be able to write into orphan
            createUserId = uid
        };

        var result = await writer.WriteAsync(content, uid);

        Assert.True(result.id > 0, "ID was not assigned to returned view!");
        Assert.Equal(content.name, result.name);
        Assert.Equal(content.content, result.content);
        Assert.Equal(content.createDate, result.createDate);
        Assert.Equal(uid, result.createUserId);
        Assert.Equal(InternalContentType.page.ToString(), result.type);
    }
}