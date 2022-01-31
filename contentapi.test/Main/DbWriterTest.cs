using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.test.Mock;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class DbWriterTest : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbUnitTestSearchFixture fixture;
    protected FakeEventQueue events;
    protected IMapper mapper;
    protected DbWriter writer;
    protected IGenericSearch searcher;


    public DbWriterTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
        this.events= new FakeEventQueue();
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), fixture.GetService<IGenericSearch>(),
            fixture.GetService<Db.ContentApiDbConnection>(), fixture.GetService<IDbTypeInfoService>(), fixture.GetService<IMapper>(),
            fixture.GetService<Db.History.IHistoryConverter>(), fixture.GetService<IPermissionService>(),
            events); 
        searcher = fixture.GetService<IGenericSearch>();

        //Reset it for every test
        fixture.ResetDatabase();
    }

    protected async Task AssertHistoryMatchesAsync(ContentView content, UserAction expected, string? message = null)
    {
        Assert.True(content.lastRevisionId > 0, "Content didn't have lastRevisionId!"); //ALL content should have a revision id
        var history = await searcher.GetById<ActivityView>(RequestType.activity, content.lastRevisionId, true);
        Assert.Equal(history.contentId, content.id);
        Assert.Equal(expected, history.action);

        if(message != null)
            Assert.Equal(message, history.message);
    }

    protected LiveEvent AssertEventMatchesBase(long id, UserAction expected, long userId, EventType type)
    {
        //NOTE: only the real checkpoint tracker assigns ids, so these ids will be 0!
        var evs = events.Events.Where(x => x.refId == id && x.action == expected && x.type == type && x.userId == userId);
        Assert.NotNull(evs);
        Assert.Single(evs);
        return evs.First();
    }
    protected void AssertContentEventMatches(ContentView content, long userId, UserAction expected)
    {
        //Ensure the events are reported correctly.
        //REMEMBER: we're looking for an ACTIVITY event, so the id is the revision id!
        var ev = AssertEventMatchesBase(content.lastRevisionId, expected, userId, EventType.activity);
        AssertDateClose(ev.date);
    }

    protected void AssertCommentEventMatches(CommentView comment, long userId, UserAction expected)
    {
        //Ensure the events are reported correctly
        var ev = AssertEventMatchesBase(comment.id, expected, userId, EventType.comment);
        AssertDateClose(ev.date);
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
        var content = GetNewPageView(parentId);

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.page);
            await AssertHistoryMatchesAsync(result, UserAction.create);
            Assert.Equal(content.content, result.content);
            AssertContentEventMatches(result, uid, UserAction.create);
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
        var content = GetNewFileView(parentId);

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.file);
            await AssertHistoryMatchesAsync(result, UserAction.create);
            Assert.Equal(content.mimetype, result.mimetype);
            Assert.Equal(content.hash, result.hash);
            Assert.Equal(content.quantization, result.quantization);
            AssertContentEventMatches(result, uid, UserAction.create);
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
        var content = GetNewModuleView(parentId);

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.module);
            await AssertHistoryMatchesAsync(result, UserAction.create);
            Assert.Equal(content.code, result.code);
            Assert.Equal(content.description, result.description);
            AssertContentEventMatches(result, uid, UserAction.create);
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
    [InlineData((int)UserVariations.Super, "U", true)]
    [InlineData(1+ (int)UserVariations.Super, "U", true)] //this is super
    [InlineData((int)UserVariations.Super, "", false)]
    [InlineData(1 + (int)UserVariations.Super, "", true)] //THIS one is super
    public async Task WriteAsync_UpdateBasic(long uid, string globalPerms, bool allowed)
    {
        //This should be a "writable by anybody" thingy
        var content = GetNewPageView(0, new Dictionary<long, string> { { 0 , globalPerms } });

        //Write by anybody OTHER THAN the users you might pick
        var writeUser = 2 + (int)UserVariations.Super;
        var original = await writer.WriteAsync(content, writeUser);
        Assert.True(original.id > 0);

        //Now we edit the view
        original.name = "SOME EDITED NAME!";

        if(allowed)
        {
            var result = await writer.WriteAsync(original, uid);
            StandardContentEqualityCheck(original, result, writeUser, InternalContentType.page);
            await AssertHistoryMatchesAsync(result, UserAction.update);
            AssertContentEventMatches(original, writeUser, UserAction.create);
            AssertContentEventMatches(result, uid, UserAction.update);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(original, uid);
            });
        }
    }

    [Fact]
    public async Task WriteAsync_HistoryAndMessage()
    {
        //This should be a "writable by anybody" thingy
        var content = GetNewPageView(0);

        //Write by anybody OTHER THAN the users you might pick
        var writeUser = 2 + (int)UserVariations.Super;
        var create = await writer.WriteAsync(content, writeUser, "The first message!");
        await AssertHistoryMatchesAsync(create, UserAction.create, "The first message!");
        //Now we edit the view
        create.name = "SOME EDITED NAME!";
        var update = await writer.WriteAsync(create, writeUser, "The update message.");
        await AssertHistoryMatchesAsync(update, UserAction.update, "The update message.");
        var delete = await writer.DeleteAsync<ContentView>(update.id, writeUser, "The delete message...");
        await AssertHistoryMatchesAsync(delete, UserAction.delete, "The delete message...");
    }

    [Theory] //No matter who you are, you can't delete things by setting the deleted field
    [InlineData((int)UserVariations.Super)]
    [InlineData(1+ (int)UserVariations.Super)] //this is super
    public async Task WriteAsync_ForbidDeleteField(long uid)
    {
        //This should be a "writable by anybody" thingy
        var content = GetNewPageView(0);

        //Write by the user themselves
        var original = await writer.WriteAsync(content, uid);
        Assert.True(original.id > 0);

        //Now we edit the view
        original.deleted = true;

        //Don't let people set deleted!
        await Assert.ThrowsAnyAsync<RequestException>(async () =>
        {
            await writer.WriteAsync(original, uid);
        });
    }

    [Theory] //NOTE: these updates using the AccessBySupers isn't about whether the user is super or not!
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_UpdateParentId(long uid, int parentId, bool allowed)
    {
        //This should be a "writable by anybody" thingy
        var content = GetNewPageView(0, new Dictionary<long, string> { { 0 , "U" } }); //Everyone can edit, but that doesn't mean everyone can put content anywhere

        //Write by anybody OTHER THAN the users you might pick
        var writeUser = 2 + (int)UserVariations.Super;
        var original = await writer.WriteAsync(content, writeUser);
        Assert.True(original.id > 0);

        //Now we edit the view
        original.parentId = parentId;

        if(allowed)
        {
            var result = await writer.WriteAsync(original, uid);
            StandardContentEqualityCheck(original, result, writeUser, InternalContentType.page);
            await AssertHistoryMatchesAsync(result, UserAction.update);
            AssertContentEventMatches(original, writeUser, UserAction.create);
            AssertContentEventMatches(result, uid, UserAction.update);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(original, uid);
            });
        }
    }

    [Theory] //NOTE: these updates using the AccessBySupers isn't about whether the user is super or not!
    [InlineData((int)UserVariations.Super)]
    [InlineData(1 + (int)UserVariations.Super)]
    public async Task WriteAsync_UpdateDeletedContent(long uid)
    {
        //This should be a "writable by anybody" thingy
        var content = GetNewPageView(0, new Dictionary<long, string> { { 0 , "U" } }); //Everyone can edit, but that doesn't mean everyone can put content anywhere

        //Write by anybody OTHER THAN the users you might pick
        var writeUser = 2 + (int)UserVariations.Super;
        var original = await writer.WriteAsync(content, writeUser);
        Assert.True(original.id > 0);

        var deleteResult = await writer.DeleteAsync<ContentView>(original.id, writeUser);
        content.id = original.id;

        //The content was deleted, so technically it's "not found"
        await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
        {
            await writer.WriteAsync(content, uid);
        });
    }

    [Theory] 
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    [InlineData(1+ (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //this is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task DeleteAsync_Basic(long uid, long contentId, bool allowed)
    {
        if(allowed)
        {
            var originalContent = await searcher.GetById<ContentView>(RequestType.content, contentId);
            var result = await writer.DeleteAsync<ContentView>(contentId, uid);
            //Even after deletion, the lastRevisionId should be set!
            await AssertHistoryMatchesAsync(result, UserAction.delete);
            AssertContentEventMatches(result, uid, UserAction.delete);
            //Remember, we can generally trust what the functions return because they should be EXACTLY from the database!
            //Testing to see if the ones from the database are exactly the same as those returned can be a different test
            //Assert.True(string.IsNullOrWhiteSpace(result.name), "Name was not cleared!");
            //NOTE: Used to test if name was cleared, now we just ensure the name isn't the same as what it was before
            Assert.NotEqual(originalContent.name, result.name);
            Assert.Empty(result.keywords);
            Assert.Empty(result.values);
            Assert.Empty(result.permissions);
            Assert.True(result.deleted, "Item wasn't marked deleted!");
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.DeleteAsync<ContentView>(contentId, uid);
            });
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, 0)]
    [InlineData(1+ (int)UserVariations.Super, 0)] //this is super
    [InlineData((int)UserVariations.Super, -1)]
    [InlineData(1+ (int)UserVariations.Super, -1)] //this is super
    public async Task DeleteAsync_DumbId(long uid, long contentId)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
        {
            await writer.DeleteAsync<ContentView>(contentId, uid);
        });
    }

    [Fact]
    public async Task DeleteAsync_ForcedBaseType()
    {
        var modUid = 1 + (int)UserVariations.Super;

        //Ensure there's something of every type in there
        var pv = await writer.WriteAsync(GetNewPageView(), 1);
        var fv = await writer.WriteAsync(GetNewFileView(), 1);
        var mv = await writer.WriteAsync(GetNewModuleView(), modUid);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await writer.DeleteAsync<PageView>(pv.id, 1);
        });
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await writer.DeleteAsync<FileView>(fv.id, 1);
        });
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
            await writer.DeleteAsync<ModuleView>(mv.id, modUid);
        });

        //These should succeed
        await writer.DeleteAsync<ContentView>(pv.id, 1);
        await writer.DeleteAsync<ContentView>(fv.id, 1);
        await writer.DeleteAsync<ContentView>(mv.id, modUid);
    }

    [Theory]
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_BasicComment(long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var comment = GetNewCommentView(parentId);

        if(allowed)
        {
            var result = await writer.WriteAsync(comment, uid);
            StandardCommentEqualityCheck(comment, result, uid);
            AssertCommentEventMatches(result, uid, UserAction.create);
            //StandardContentEqualityCheck(, result, uid, InternalContentType.page);
            //Assert.Equal(content.content, result.content);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(comment, uid);
            });
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, true)] //User can edit their own comment
    [InlineData((int)UserVariations.Super, 1+ (int)UserVariations.Super, true)] //Supers can edit anybody's comment
    [InlineData(1 + (int)UserVariations.Super, 1 + (int)UserVariations.Super, true)] //Supers can edit their own comment
    [InlineData(1 + (int)UserVariations.Super, (int)UserVariations.Super, false)] //Users can't edit other people's comments
    public async Task WriteAsync_BasicUpdateComment(long poster, long editor, bool allowed)
    {
        var comment = GetNewCommentView(1 + (int)ContentVariations.AccessByAll);
        var written = await writer.WriteAsync(comment, poster);
        StandardCommentEqualityCheck(comment, written, poster);

        //Now try to edit it!
        written.text = "Something new`!!";

        if(allowed)
        {
            var result = await writer.WriteAsync(written, editor);
            AssertDateClose(result.editDate ?? throw new InvalidOperationException("NO EDIT DATE SET"));
            StandardCommentEqualityCheck(written, result, poster); //Original poster should be preserved
            AssertCommentEventMatches(written, poster, UserAction.create);
            AssertCommentEventMatches(result, editor, UserAction.update);
            Assert.Equal(editor, result.editUserId);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(written, editor);
            });
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, true)] //User can edit their own comment
    [InlineData((int)UserVariations.Super, 1+ (int)UserVariations.Super, true)] //Supers can edit anybody's comment
    [InlineData(1 + (int)UserVariations.Super, 1 + (int)UserVariations.Super, true)] //Supers can edit their own comment
    [InlineData(1 + (int)UserVariations.Super, (int)UserVariations.Super, false)] //Users can't edit other people's comments
    public async Task WriteAsync_BasicDeleteComment(long poster, long editor, bool allowed)
    {
        var comment = GetNewCommentView(1 + (int)ContentVariations.AccessByAll);
        var written = await writer.WriteAsync(comment, poster);
        StandardCommentEqualityCheck(comment, written, poster);

        //Now try to delete it!
        if(allowed)
        {
            var result = await writer.DeleteAsync<CommentView>(written.id, editor);
            AssertCommentEventMatches(written, poster, UserAction.create);
            AssertCommentEventMatches(result, editor, UserAction.delete);
            Assert.True(result.deleted);
            //Assert.True(string.IsNullOrEmpty(result.text));
            //Note: USED to check for empty comment content, now just make sure it's not what it was before
            Assert.NotEqual(comment.text, result.text);
            Assert.True(result.id > 0);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.DeleteAsync<CommentView>(written.id, editor);
            });
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, 0)] //Nobody can write to nothing!
    [InlineData(1 + (int)UserVariations.Super, 0)] //THIS one is super
    public async Task WriteAsync_ForbidOrphanedComment(long uid, long parentId)
    {
        var comment = GetNewCommentView(parentId);

        await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
        {
            await writer.WriteAsync(comment, uid);
        });
    }

    [Theory] //No matter who you are, you can't delete things by setting the deleted field
    [InlineData((int)UserVariations.Super)]
    [InlineData(1+ (int)UserVariations.Super)] //this is super
    public async Task WriteAsync_ForbidDeleteField_Comment(long uid)
    {
        //This should be a "writable by anybody" thingy
        var comment = GetNewCommentView(1 + (int)ContentVariations.AccessByAll);

        //Write by the user themselves
        var original = await writer.WriteAsync(comment, uid);
        Assert.True(original.id > 0);

        //Now we edit the view
        original.deleted = true;

        //Don't let people set deleted!
        await Assert.ThrowsAnyAsync<RequestException>(async () =>
        {
            await writer.WriteAsync(original, uid);
        });
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
            await writer.ValidatePermissionFormat(perms);
            Assert.True(true); //Not necessary but whatever
        }
        else
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () => await writer.ValidatePermissionFormat(perms));
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