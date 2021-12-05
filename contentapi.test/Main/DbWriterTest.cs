using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
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
        var content = new ContentView {
            createUserId = uid
        };

        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
            await writer.WriteAsync(content, uid);
        });
    }

    [Fact]
    public async Task WriteAsync_MostSimple()
    {

        Assert.Equal(fixture.ContentCount, (await searcher.QueryRaw("select * from content", new Dictionary<string, object>())).Count());
    }
}