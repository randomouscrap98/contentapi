
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.test.Mock;
using contentapi.Views;
using Xunit;

namespace contentapi.test;

public class ViewUnitTestBase : UnitTestBase
{
    public ViewUnitTestBase()
    {

    }

    protected void AssertKeywordsEqual(ContentView original, ContentView result)
    {
        Assert.True(original.keywords.OrderBy(c => c).SequenceEqual(result.keywords.OrderBy(c => c)), "Keywords were changed!");
    }

    protected void AssertValuesEqual(ContentView original, ContentView result)
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
    protected void AssertPermissionsNormal(ContentView original, ContentView result)
    {
        original.permissions[original.createUserId] = "CRUD";

        Assert.Equal(original.permissions.Count, result.permissions.Count);
        foreach(var perm in original.permissions)
        {
            Assert.True(result.permissions.ContainsKey(perm.Key), "Permission from original not found in result!");
            Assert.Equal(perm.Value, result.permissions[perm.Key]);
        }
    }

    protected void StandardContentEqualityCheck(ContentView original, ContentView result, long uid, InternalContentType expectedType)
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

    protected void StandardCommentEqualityCheck(CommentView original, CommentView result, long uid)
    {
        AssertDateClose(result.createDate);
        Assert.True(result.id > 0, "ID was not assigned to returned view!");
        Assert.Equal(original.text, result.text);
        Assert.Equal(uid, result.createUserId);
        Assert.Equal(original.contentId, result.contentId);
    }

    protected PageView GetNewPageView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new PageView {
            name = "whatever",
            content = "Yeah this is content!",
            parentId = parentId,
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } }
        };
    }

    protected FileView GetNewFileView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new FileView {
            name = "whatever",
            mimetype = "image/png",
            quantization = "10",
            parentId = parentId,
            hash = "babnana",
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } },
        };
    }

    public ModuleView GetNewModuleView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new ModuleView {
            name = "whatever",
            code = "Yeah this is... code? [beep boop] />?{Fd?>FDSI#!@$F--|='\"_+",
            description = "Aha! An extra field!",
            parentId = parentId,
            values = new Dictionary<string, string> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } },
        };
    }

    protected CommentView GetNewCommentView(long contentId)//, Dictionary<long, string>? permissions = null)
    {
        return new CommentView {
            text = "Yeah this is comment!",
            contentId = contentId,
        };
    }
}