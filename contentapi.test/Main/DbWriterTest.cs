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
    protected IMapper mapper;

    public DbWriterTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
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

    private void AssertDateClose(DateTime dt1, DateTime? dt2 = null, double seconds = 5)
    {
        var dt2r = dt2 ?? DateTime.UtcNow;
        Assert.True(Math.Abs((dt1 - dt2r).TotalSeconds) < seconds, $"Dates were not within an acceptable closeness in range! DT1: {dt1}, DT2: {dt2r}");
    }

    private void AssertKeywordsEqual(ContentView original, ContentView result)
    {
        Assert.True(original.keywords.OrderBy(c => c).SequenceEqual(result.keywords.OrderBy(c => c)), "Keywords were changed!");
    }

    private void AssertValuesEqual(ContentView original, ContentView result)
    {
        Assert.Equal(original.values.Count, result.values.Count);
        foreach(var value in original.values)
        {
            Assert.True(result.values.ContainsKey(value.Key), $"Value {value.Key} from original not found in result!");
            Assert.Equal(value.Value, result.values[value.Key]);
        }
    }

    /// <summary>
    /// WARN: THIS MODIFIES THE ORIGINAL CONTENT'S PERMISSIONS!
    /// </summary>
    /// <param name="original"></param>
    /// <param name="result"></param>
    private void AssertPermissionsNormal(ContentView original, ContentView result)
    {
        original.permissions[original.createUserId] = "CRUD";

        Assert.Equal(original.permissions.Count, result.permissions.Count);
        foreach(var perm in original.permissions)
        {
            Assert.True(result.permissions.ContainsKey(perm.Key), "Permission from original not found in result!");
            Assert.Equal(perm.Value, result.permissions[perm.Key]);
        }
    }

    private void StandardContentEqualityCheck(ContentView original, ContentView result, long uid, InternalContentType expectedType)
    {
        AssertDateClose(result.createDate);
        Assert.True(result.id > 0, "ID was not assigned to returned view!");
        Assert.Equal(original.name, result.name);
        Assert.Equal(uid, result.createUserId);
        Assert.Equal(original.parentId, result.parentId);
        Assert.Equal(expectedType, result.internalType);
        AssertKeywordsEqual(original, result);
        AssertValuesEqual(original, result);
        AssertPermissionsNormal(original, result);
    }

    [Theory]
    [InlineData((int)UserVariations.Super)]
    [InlineData(1 + (int)UserVariations.Super)] //THIS one is super
    public async Task WriteAsync_CantWriteRaw(long uid)
    {
        var content = new ContentView {
            name = "Yeah",
            parentId = 0,
            createUserId = uid
        };

        //Don't care what type, but it should fail somehow...
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            await writer.WriteAsync(content, uid);
        });
    }

    //This tests whether supers and non supers can both write orphaned pages AND write into 
    //existing pages that have access to all.
    [Theory]
    [InlineData((int)UserVariations.Super, 0, true)]
    [InlineData(1 + (int)UserVariations.Super, 0, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_BasicPage(long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var content = new PageView {
            name = "whatever",
            content = "Yeah this is content!",
            parentId = parentId,
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = new Dictionary<long, string> { { 0 , "CR" } },
            createUserId = uid
        };

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.page);
            Assert.Equal(content.content, result.content);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(content, uid);
            });
        }
    }

    //This tests whether supers and non supers can both write orphaned pages AND write into 
    //existing pages that have access to all.
    [Theory]
    [InlineData((int)UserVariations.Super, 0, true)]
    [InlineData(1 + (int)UserVariations.Super, 0, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_BasicFile(long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var content = new FileView {
            name = "whatever",
            mimetype = "image/png",
            quantization = "10",
            parentId = parentId,
            hash = "babnana",
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = new Dictionary<long, string> { { 0 , "CR" } },
            createUserId = uid
        };

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.file);
            Assert.Equal(content.mimetype, result.mimetype);
            Assert.Equal(content.hash, result.hash);
            Assert.Equal(content.quantization, result.quantization);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(content, uid);
            });
        }
    }

    [Theory] //For modules, regular users can NEVER create!
    [InlineData((int)UserVariations.Super, 0, false)]
    [InlineData(1 + (int)UserVariations.Super, 0, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_BasicModule(long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var content = new ModuleView {
            name = "whatever",
            code = "Yeah this is... code? [beep boop] />?{Fd?>FDSI#!@$F--|='\"_+",
            description = "Aha! An extra field!",
            parentId = parentId,
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = new Dictionary<long, string> { { 0 , "CR" } },
            createUserId = uid
        };

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.module);
            Assert.Equal(content.code, result.code);
            Assert.Equal(content.description, result.description);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(content, uid);
            });
        }
    }

    [Theory]
    [InlineData(1, "CRUD", true)]
    [InlineData(0, "CR", true)]
    [InlineData(2, "ud", true)]
    [InlineData(1, " uR dC ", true)]
    [InlineData(1, "CRUT", false)]
    [InlineData(1, "#@%$", false)]
    [InlineData(999999, "CRUD", false)] //Something that DEFINITELY isn't a user!
    public async Task CheckPermissionValidityAsync(long uid, string crud, bool valid)
    {
        var perms = new Dictionary<long, string> { { uid, crud } };

        if(valid)
        {
            await writer.CheckPermissionValidityAsync(perms);
            Assert.True(true); //Not necessary but whatever
        }
        else
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await writer.CheckPermissionValidityAsync(perms));
        }
    }

    [Fact]
    public async Task WriteAsync_BadType_Activity()
    {
        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => await writer.WriteAsync(new ActivityView() { userId = 1 }, 1));
    }

    [Fact]
    public async Task WriteAsync_BadType_AdminLog()
    {
        await Assert.ThrowsAnyAsync<ForbiddenException>(async () => await writer.WriteAsync(new AdminLogView() { initiator = 1, target = 1 }, 1));
    }
}