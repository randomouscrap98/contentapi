using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.data;
using contentapi.Db;
using contentapi.History;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class ContentHistoryConverterTest : UnitTestBase
{
    public HistoryConverter converter;

    public ContentHistoryConverterTest()
    {
        converter = new HistoryConverter(GetService<ILogger<HistoryConverter>>());
    }

    public ContentSnapshot GetGenericSnapshot()
    {
        return new ContentSnapshot()
        {
            id = 123,
            name = "yeah",
            createDate = DateTime.UtcNow,
            createUserId = 34,
            description = "this is a short thing",
            text = "this is supposed to be a long thingg\nbut it's whatever",
            values = new List<ContentValue>() {
                new ContentValue() { id = 33, key = "first", value = "something"},
                new ContentValue() { id = 34, key = "second", value = "else" }
            },
            keywords = new List<ContentKeyword> {
                new ContentKeyword() { id = 65, value = "dog"},
                new ContentKeyword() { id = 66, value = "cat" },
                new ContentKeyword() { id = 67, value = "shouldn't have spaces but here we are" }
            },
            permissions = new List<ContentPermission> {
                new ContentPermission() { id = 99, userId = 34, create = true, update = true, delete = true, read = true },
                new ContentPermission() { id = 100, userId = 0, create = false, update = false, delete = false, read = true }
            }
        };
    }

    [Fact]
    public async Task V1SnapshotTransparent()
    {
        var snapshot = GetGenericSnapshot();
        var compressed = await converter.GetV1Snapshot(snapshot);
        Assert.True(compressed.Length > 0);
        var result = await converter.ExtractV1Snapshot<ContentSnapshot>(compressed);
        AssertSnapshotsEqual(snapshot, result);
    }

    [Fact]
    public async Task ActualUsageTransparent()
    {
        var snapshot = GetGenericSnapshot();
        var history = await converter.ContentToHistoryAsync(snapshot, 123, UserAction.create);
        Assert.Equal(123, history.createUserId);
        Assert.Equal(UserAction.create, history.action);
        Assert.True(history.snapshot.Length > 0);
        var result = await converter.HistoryToContentAsync(history);
        AssertSnapshotsEqual(snapshot, result);
    }
}