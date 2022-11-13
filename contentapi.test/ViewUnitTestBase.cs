
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using contentapi.data;
using contentapi.Db;
using contentapi.data.Views;
using Xunit;

namespace contentapi.test;

public class ViewUnitTestBase : UnitTestBase
{
    protected const long SuperUserId = 1 + (int)UserVariations.Super;
    protected const long NormalUserId = (int)UserVariations.Super;
    protected const long AllAccessContentId = 1 + (int)ContentVariations.AccessByAll;
    protected const long AllAccessContentId2 = 1 + (int)ContentVariations.AccessByAll + (int)ContentVariations.Values;
    protected const long SuperAccessContentId = 1 + (int)ContentVariations.AccessBySupers;

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
    /// Make sure the permissions match, or that they look as expected (for instance, the create user has ALL perms)
    /// </summary>
    /// <param name="original"></param>
    /// <param name="result"></param>
    protected void AssertPermissionsNormal(ContentView original, ContentView result)
    {
        //Want to make sure 
        //original.permissions[original.createUserId] = "CRUD";

        Assert.Equal(original.permissions.Count, result.permissions.Count);
        foreach(var perm in original.permissions)
        {
            Assert.True(result.permissions.ContainsKey(perm.Key), "Permission from original not found in result!");

            //We can trust the createUserId at this point... or at least, we hope
            if(perm.Key == result.createUserId)
                Assert.Equal("CRUD", result.permissions[perm.Key]);
            else
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
        Assert.Equal(expectedType, result.contentType);
        AssertKeywordsEqual(original, result);
        AssertValuesEqual(original, result);
        AssertPermissionsNormal(original, result);
    }

    protected void StandardUserEqualityCheck(UserView original, UserView result, long uid)
    {
        Assert.True(result.id > 0, "ID was not assigned to returned view!");
        Assert.Equal(original.username, result.username);
        Assert.Equal(original.special, result.special);
        Assert.Equal(original.createDate, result.createDate);
        Assert.Equal(original.id, result.id);
        Assert.Equal(original.avatar, result.avatar);
        Assert.Equal(original.registered, result.registered);
        Assert.Equal(original.super, result.super);
        Assert.True(original.groups.OrderBy(x => x).SequenceEqual(result.groups.OrderBy(x => x)), $"Groups not the same in user {result.id}!");
    }

    protected void StandardCommentEqualityCheck(MessageView original, MessageView result, long uid)
    {
        AssertDateClose(result.createDate);
        Assert.True(result.id > 0, "ID was not assigned to returned view!");
        Assert.Equal(original.text, result.text);
        Assert.Equal(uid, result.createUserId);
        Assert.Equal(original.contentId, result.contentId);
    }

    protected ContentView GetNewPageView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new ContentView {
            name = "whatever",
            contentType = InternalContentType.page,
            text = "Yeah this is content!",
            parentId = parentId,
            values = new Dictionary<string, object> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } }
        };
    }

    protected ContentView GetNewFileView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new ContentView {
            name = "whatever",
            contentType = InternalContentType.file,
            literalType = "image/png",
            meta = "{\"quantization\":10}",
            parentId = parentId,
            //hash = "babnana", //We specifically set a hash 
            values = new Dictionary<string, object> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } },
        };
    }

    public ContentView GetNewModuleView(long parentId = 0, Dictionary<long, string>? permissions = null)
    {
        return new ContentView {
            name = "whatever",
            contentType = InternalContentType.module,
            text = "Yeah this is... code? [beep boop] />?{Fd?>FDSI#!@$F--|='\"_+",
            description = "Aha! An extra field!",
            parentId = parentId,
            values = new Dictionary<string, object> { { "one" , "thing" }, { "kek", "macaroni and things" } },
            keywords = new List<string> { "heck", "heck2", "dead" },
            permissions = permissions ?? new Dictionary<long, string> { { 0 , "CR" } },
        };
    }

    public MessageView GetNewCommentView(long contentId)//, Dictionary<long, string>? permissions = null)
    {
        return new MessageView {
            text = "Yeah this is comment!",
            contentId = contentId,
            module = null
        };
    }

    public MessageEngagementView GetMessageEngagementView(long messageId, string type, string engagement)
    {
        return new MessageEngagementView() {
            messageId = messageId,
            type = type,
            engagement = engagement 
        };
    }
}