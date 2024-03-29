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
using contentapi.data;
using contentapi.data.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class DbWriterTest : ViewUnitTestBase, IDisposable
{
    protected DbUnitTestSearchFixture fixture;
    protected FakeEventQueue events;
    protected IMapper mapper;
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbWriterConfig config;
    protected Random random = new Random();
    protected RandomGenerator rng;
    protected IViewTypeInfoService typeInfoService;


    public DbWriterTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
        this.events= new FakeEventQueue();
        this.config = new DbWriterConfig();
        this.rng = new RandomGenerator();
        this.typeInfoService = fixture.GetService<IViewTypeInfoService>();
        this.searcher = fixture.GetGenericSearcher();
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), searcher,
            fixture.GetConnection(), typeInfoService, fixture.GetService<IMapper>(),
            fixture.GetService<History.IHistoryConverter>(), fixture.GetService<IPermissionService>(),
            events, config, rng, fixture.GetService<IUserService>());

        //Reset it for every test
        fixture.ResetDatabase();
    }

    public void Dispose()
    {
        //Since we created our own writer
        writer.Dispose();
    }

    protected async Task AssertHistoryMatchesAsync(ContentView content, UserAction expected, string? message = null)
    {
        Assert.True(content.lastRevisionId > 0, "Content didn't have lastRevisionId!"); //ALL content should have a revision id
        var history = await searcher.GetById<ActivityView>(content.lastRevisionId, true);
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
        AssertDateClose(evs.First().date);
        return evs.First();
    }

    protected void AssertContentEventMatches(ContentView content, long userId, UserAction expected)
    {
        //Ensure the events are reported correctly.
        //REMEMBER: we're looking for an ACTIVITY event, so the id is the revision id!
        var ev = AssertEventMatchesBase(content.lastRevisionId, expected, userId, EventType.activity_event);
    }

    protected void AssertCommentEventMatches(MessageView comment, long userId, UserAction expected)
    {
        //Ensure the events are reported correctly
        var ev = AssertEventMatchesBase(comment.id, expected, userId, EventType.message_event);
    }

    protected void AssertUserEventMatches(UserView user, long userId, UserAction expected)
    {
        var ev = AssertEventMatchesBase(user.id, expected, userId, EventType.user_event);
    }

    protected void AssertWatchEventMatches(WatchView watch, long userId, UserAction expected)
    {
        var ev = AssertEventMatchesBase(watch.id, expected, userId, EventType.watch_event);
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
            Assert.Equal(content.text, result.text);
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

    [Fact]
    public async Task WriteAsync_Page_NoValuesKeywordsPermissions() //long uid, long parentId, bool allowed)
    {
        var uid = (int)UserVariations.Super;

        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var content = GetNewPageView();
        content.values.Clear();
        content.permissions.Clear();
        content.keywords.Clear();

        var result = await writer.WriteAsync(content, uid);
        StandardContentEqualityCheck(content, result, uid, InternalContentType.page);
        await AssertHistoryMatchesAsync(result, UserAction.create);
        Assert.Equal(content.text, result.text);
        AssertContentEventMatches(result, uid, UserAction.create);
    }

    //This tests whether supers and non supers can both write orphaned pages AND write into 
    //existing pages that have access to all.
    //HAHA GUESS WHAT: NOBODY CAN CREATE FILES! Updated with single typification
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
        content.hash = "somethinglong"; //Need a SPECIFIC hash so we can test it

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, InternalContentType.file);
            await AssertHistoryMatchesAsync(result, UserAction.create);
            Assert.Equal(content.literalType, result.literalType);
            Assert.Equal(content.hash, result.hash);
            Assert.Equal(content.meta, result.meta);
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

    [Theory] //For modules and system types, regular users can NEVER create!
    [InlineData(InternalContentType.module, (int)UserVariations.Super, 0, false)]
    [InlineData(InternalContentType.module, 1 + (int)UserVariations.Super, 0, true)] //THIS one is super
    [InlineData(InternalContentType.module, (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, false)]
    [InlineData(InternalContentType.module, 1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData(InternalContentType.module, (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(InternalContentType.module, 1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    [InlineData(InternalContentType.system, (int)UserVariations.Super, 0, false)]
    [InlineData(InternalContentType.system, 1 + (int)UserVariations.Super, 0, true)] //THIS one is super
    [InlineData(InternalContentType.system, (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, false)]
    [InlineData(InternalContentType.system, 1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    [InlineData(InternalContentType.system, (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    [InlineData(InternalContentType.system, 1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_BasicSystemTypes(InternalContentType type, long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var content = GetNewModuleView(parentId);
        content.contentType = type;

        if(allowed)
        {
            var result = await writer.WriteAsync(content, uid);
            StandardContentEqualityCheck(content, result, uid, type);
            await AssertHistoryMatchesAsync(result, UserAction.create);
            Assert.Equal(content.text, result.text);
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
            //NOTE: this USED to be "empty" because we used to clear out ALL the associated data, but now we 
            //specifically leave the permissions alone.
            Assert.NotEmpty(result.permissions);
            //Assert.Empty(result.permissions);
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
        await Assert.ThrowsAnyAsync<NotFoundException>(async () =>
        {
            await writer.DeleteAsync<ContentView>(contentId, uid);
        });
    }

    //NOTE: this test USED to check if you couldn't delete base types, but 
    //now is just another deletion test for all types.
    [Fact]
    public async Task DeleteAsync_ForcedBaseType()
    {
        var modUid = 1 + (int)UserVariations.Super;

        //Ensure there's something of every type in there
        var pv = await writer.WriteAsync(GetNewPageView(), 1);
        var fv = await writer.WriteAsync(GetNewFileView(), 1);
        var mv = await writer.WriteAsync(GetNewModuleView(), modUid);

        //await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
        //    await writer.DeleteAsync<PageView>(pv.id, 1);
        //});
        //await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
        //    await writer.DeleteAsync<FileView>(fv.id, 1);
        //});
        //await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => {
        //    await writer.DeleteAsync<ModuleView>(mv.id, modUid);
        //});

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
            var result = await writer.DeleteAsync<MessageView>(written.id, editor);
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
                await writer.DeleteAsync<MessageView>(written.id, editor);
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
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, true)]        //Can users update themselves? yes
    [InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super, true)]    //Can supers update other users? yes
    [InlineData((int)UserVariations.Super + 2, (int)UserVariations.Super, false)]   //Non-supers can't update other users
    [InlineData((int)UserVariations.Super, 1 + (int)UserVariations.Registered * 2, false)]        //WARN: A HACK! Only works if Registered is the last user variation! This ensures users can't update groups
    [InlineData((int)UserVariations.Super + 1, 1 + (int)UserVariations.Registered * 2, true)]     //Supers can update groups
    public async Task WriteAsync_BasicUpdateUserAndGroup(long writerId, long userId, bool allowed)
    {
        var user = await searcher.GetById<UserView>(RequestType.user, userId);

        //Modify fields we're allowed to modify
        user.username = "somethingNEW";
        user.avatar = "0";  //WE ARE ASSUMING THE DEFAULT HASH IS 0!!!
        user.special = "somethingSTUPID";

        if(allowed)
        {
            var result = await writer.WriteAsync(user, writerId);
            StandardUserEqualityCheck(user, result, writerId);
            AssertUserEventMatches(result, writerId, UserAction.update);
            //AssertCommentEventMatches(result, uid, UserAction.create);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(user, writerId);
            });
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, true, false)]     //Non-supers can't do anything with the super field, regardless of who they're doing it to
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, false, true)]     //BUT, setting super to false when it's already false won't be noticed anyway...
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super + 1, true, false)]  
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super + 1, false, false)] 
    [InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super, true, true)]     //supers can do anything with the super field, regardless of who they're doing it to
    [InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super, false, true)] 
    [InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 1, true, true)]  
    [InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 1, false, true)] 
    public async Task WriteAsync_SettingSuper(long writerId, long userId, bool super, bool allowed)
    {
        var user = await searcher.GetById<UserView>(RequestType.user, userId);

        //Modify fields we're allowed to modify
        user.super = super;

        if(allowed)
        {
            var result = await writer.WriteAsync(user, writerId);
            StandardUserEqualityCheck(user, result, writerId);
            AssertUserEventMatches(result, writerId, UserAction.update);
            Assert.Equal(super, result.super);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(user, writerId);
            });
        }
    }

    //THIS MAKES THE ASSUMPTION THAT "0" IS THE DEFAULT HASH!
    [Theory]
    [InlineData(NormalUserId, "normal-hash", "0", true)]
    [InlineData(NormalUserId, "normal-hash", "normal-hash", true)]
    [InlineData(NormalUserId, "normal-hash", "bad-hash", false)]
    [InlineData(SuperUserId, "normal-hash", "bad-hash", false)]
    public async Task WriteAsync_SettingAvatar(long userId, string writeFileHash, string avatar, bool allowed)
    {
        var file = GetNewFileView();
        file.hash = writeFileHash;

        await writer.WriteAsync(file, SuperUserId); //This ensures there is SOMe file with a hash we can expect
        var user = await searcher.GetById<UserView>(RequestType.user, userId);
        user.avatar = avatar;

        if(allowed)
        {
            var result = await writer.WriteAsync(user, userId);
            StandardUserEqualityCheck(user, result, userId); //This also checks avatar but
            Assert.Equal(avatar, result.avatar);
        }
        else
        {
            await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(user, userId));
        }
    }

    [Theory]
    [InlineData((int)UserVariations.Super, UserType.user, false, false)]        //Nobody can create users
    [InlineData((int)UserVariations.Super, UserType.user, true, false)]         
    [InlineData((int)UserVariations.Super + 1, UserType.user, false, false)]    
    [InlineData((int)UserVariations.Super + 1, UserType.user, true, false)]    
    [InlineData((int)UserVariations.Super, UserType.group, false, true)]        //Users can create groups (for now)
    [InlineData((int)UserVariations.Super, UserType.group, true, false)]         
    [InlineData((int)UserVariations.Super + 1, UserType.group, false, true)]     //Supers can create groups (regardless of super)
    [InlineData((int)UserVariations.Super + 1, UserType.group, true, true)]     
    public async Task WriteAsync_NewUser_Gamut(long creatorId, UserType type, bool super, bool allowed) //long uid, long parentId, bool allowed)
    {
        //I think this is all you really need... maybe
        var user = new UserView()
        {
            username = "whatever_dude",
            type = type,
            super = super
        };

        if(allowed)
        {
            var result = await writer.WriteAsync(user, creatorId);
            AssertDateClose(result.createDate);
            Assert.Equal(user.username, result.username);
            Assert.Equal(user.type, result.type);
            Assert.Equal(user.super, result.super);
            Assert.Equal(creatorId, result.createUserId);

            //"Users" created through this endpoint should maaaybe be registered. This also ensures that, even if there are weird bugs,
            //these users probably can't login
            Assert.False(user.registered, $"New user {result.username} is somehow immediately registered!"); 
            
            AssertUserEventMatches(result, creatorId, UserAction.create);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.WriteAsync(user, creatorId);
            });
        }
    }

    [Theory]
    [InlineData(NormalUserId, NormalUserId, true)]
    [InlineData(NormalUserId, SuperUserId + 1, false)] //A non-super trying to edit someone else's group
    [InlineData(NormalUserId, SuperUserId, true)]
    [InlineData(SuperUserId, SuperUserId, true)]
    [InlineData(SuperUserId, NormalUserId, false)]
    public async Task WriteAsync_GroupEdit_Allowed(long creatorId, long editor, bool allowed)
    {
        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group
        };

        //Write the initial group, should always be fine
        var result = await writer.WriteAsync(group, creatorId);
        Assert.Empty(result.groups);
        Assert.Empty(result.usersInGroup);

        //Now change the special field or something
        result.special = "wow it's new! new!!!";

        if(allowed)
        {
            var result2 = await writer.WriteAsync(result, editor);
            Assert.Equal(result.id, result2.id);
            Assert.Equal(result.special, result2.special);
            Assert.Empty(result2.groups);
            Assert.Empty(result2.usersInGroup);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(result, editor));
        }
    }

    [Theory]
    [InlineData(NormalUserId, NormalUserId, false)] //NOBODY can add themselves to a group through the "groups" field! It's readonly!
    [InlineData(NormalUserId, SuperUserId, false)]
    [InlineData(SuperUserId, NormalUserId, false)]
    [InlineData(SuperUserId, SuperUserId, false)]
    public async Task WriteAsync_AddSelfToGroup_Fail(long creatorId, long editor, bool allowed)
    {
        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group
        };

        //Write the initial group, should always be fine
        var result = await writer.WriteAsync(group, creatorId);
        Assert.True(result.id > 0);

        var untoucheduser = await searcher.GetById<UserView>(RequestType.user, editor);
        var user = await searcher.GetById<UserView>(RequestType.user, editor);
        user.groups.Add(result.id);

        //It should probably always work
        var result2 = await writer.WriteAsync(user, editor);

        if(allowed)
            Assert.Contains(result.id, result2.groups);
        else
            Assert.True(untoucheduser.groups.OrderBy(x => x).SequenceEqual(result2.groups.OrderBy(x => x)));
    }

    private Task<UserView> MakeQuickGroup(bool super)
    {
        var baseGroup = new UserView()
        {
            username = "test_group",
            type = UserType.group,
            super = super
        };

        return writer.WriteAsync(baseGroup, (int)UserVariations.Super + 1);
    }

    //[Theory]
    //[InlineData((int)UserVariations.Super, (int)UserVariations.Super, false, true)]
    //[InlineData((int)UserVariations.Super, (int)UserVariations.Super, true, false)]
    //[InlineData((int)UserVariations.Super, (int)UserVariations.Super + 2, false, false)]
    //[InlineData((int)UserVariations.Super, (int)UserVariations.Super + 2, true, false)]
    //[InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 1, false, true)]
    //[InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 1, true, true)]
    //[InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 2, false, true)]
    //[InlineData((int)UserVariations.Super + 1, (int)UserVariations.Super + 2, true, true)]
    //public async Task WriteAsync_AddGroup_Gamut(long updaterId, long userId, bool groupSuper, bool allowed)
    //{
    //    var group = await MakeQuickGroup(groupSuper);

    //    //go lookup the user to recieve the group
    //    var user = await searcher.GetById<UserView>(RequestType.user, userId, true);

    //    //Set it now
    //    user.groups.Add(group.id);

    //    //The function which does the update
    //    var addGroup = new Func<Task<UserView>>(() => writer.WriteAsync(user, updaterId));

    //    //Now, try to add this group to the given user BY the given user.
    //    if(allowed)
    //    {
    //        var result = await addGroup();
    //        Assert.Contains(group.id, result.groups);
    //        StandardUserEqualityCheck(user, result, userId); //Some sanity checks, should work
    //    }
    //    else
    //    {
    //        await Assert.ThrowsAnyAsync<ForbiddenException>(addGroup);
    //    }
    //}

    [Theory]
    [InlineData(NormalUserId, NormalUserId)]
    [InlineData(SuperUserId, NormalUserId)]
    [InlineData(SuperUserId, SuperUserId)]
    public async Task WriteAsync_AddUsersToUsers_Fail(long userId, long addUserId)
    {
        //go lookup the user to recieve the group
        var user = await searcher.GetById<UserView>(RequestType.user, userId, true);
        Assert.Empty(user.usersInGroup);
        user.usersInGroup.Add(addUserId);

        await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(user, user.id));
    }

    [Fact]
    public async Task WriteAsync_AddGroup_FailAndSucceed()
    {
        //Quickly create a simple group. This HAS to be done by the super user
        var group = await MakeQuickGroup(false);

        Assert.Empty(group.usersInGroup);
        group.usersInGroup.Add(9999);

        //Because 9999 isn't a user
        await Assert.ThrowsAnyAsync<ArgumentException>(() => writer.WriteAsync(group, SuperUserId));

        //But then just for fun, add the real user. This will verify that our earlier failure was most likely genuine
        group.usersInGroup.Clear();
        group.usersInGroup.Add(NormalUserId);

        var result = await writer.WriteAsync(group, SuperUserId);
        Assert.Contains(NormalUserId, result.usersInGroup);
        StandardUserEqualityCheck(group, result, SuperUserId); //Some sanity checks, should work

        //Go see if the user has that group in their list
        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId);
        Assert.Contains(group.id, user.groups);
    }

    [Theory]
    [InlineData((int)UserVariations.Super, (int)UserVariations.Super, false)] //Users can't delete themselves
    [InlineData((int)UserVariations.Super, 1+ (int)UserVariations.Super, true)] //Supers can delete them though
    [InlineData(1 + (int)UserVariations.Super, 1 + (int)UserVariations.Super, true)] //Supers can delete themselves? Maybe...
    [InlineData(1 + (int)UserVariations.Super, (int)UserVariations.Super, false)] //Users can't delete other users
    public async Task WriteAsync_BasicUserDelete(long userId, long deleterId, bool allowed)
    {
        //var comment = GetNewCommentView(1 + (int)ContentVariations.AccessByAll);
        //var written = await writer.WriteAsync(comment, poster);
        //StandardCommentEqualityCheck(comment, written, poster);
        var user = await searcher.GetById<UserView>(RequestType.user, userId, true);

        Assert.False(user.deleted);

        //Now try to delete it!
        if(allowed)
        {
            var result = await writer.DeleteAsync<UserView>(userId, deleterId);
            Assert.Empty(result.special);
            Assert.Empty(result.avatar);
            Assert.False(result.super); //Regardless of if they WERE super in the past, no information left!!
            Assert.NotEqual(user.username, result.username); //Obscure the username SOMEHOW
            Assert.True(result.deleted);
            Assert.True(result.id > 0);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () =>
            {
                await writer.DeleteAsync<UserView>(userId, deleterId);
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

    [Fact]
    //[InlineData((int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)]
    //[InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessByAll + 1, true)] //THIS one is super
    //[InlineData((int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, false)]
    //[InlineData(1 + (int)UserVariations.Super, (int)ContentVariations.AccessBySupers + 1, true)] //THIS one is super
    public async Task WriteAsync_Comment_Values() //long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var contentId = (int)ContentVariations.AccessByAll + 1;
        var uid = (int)UserVariations.Super; //NOT SUPER, NOT + 1
        var comment = GetNewCommentView(contentId);

        //Set values
        comment.values.Add("m", "12y");
        comment.values.Add("a", "1234");

        var result = await writer.WriteAsync(comment, uid);
        StandardCommentEqualityCheck(comment, result, uid);

        //Good, now the comments stored the values initially just fine
        Assert.Equal(comment.values.Count, result.values.Count);
        Assert.Equal(comment.values["m"], result.values["m"]);
        Assert.Equal(comment.values["a"], result.values["a"]);

        //And now do a simple update, make sure the values are still there
        result.text = "this is an edit";
        var result2 = await writer.WriteAsync(result, uid);

        //This ensures the update doesn't modify the values
        Assert.Equal(2, result2.values.Count);
        Assert.Equal(comment.values["m"], result2.values["m"]);
        Assert.Equal(comment.values["a"], result2.values["a"]);

        //Now modify one value, delete one, and add another
        result2.values.Remove("m");
        result2.values["a"] = "9999";
        result2.values.Add("crap", "this is crap");

        result = await writer.WriteAsync(result2, uid);
        Assert.Equal(2, result.values.Count);
        Assert.Equal(result2.values["a"], result.values["a"]);
        Assert.Equal(result2.values["crap"], result.values["crap"]);

        //And if you delete it, the values should go away
        result = await writer.DeleteAsync<MessageView>(result.id, uid);
        Assert.Empty(result.values);

        result = await searcher.GetById<MessageView>(RequestType.message, result.id);
        Assert.Empty(result.values);
    }

    [Theory]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, AllAccessContentId, true)] //THIS one is super
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, SuperAccessContentId, true)] //THIS one is super
    public async Task WriteAsync_DisallowModuleMessage(long uid, long parentId, bool allowed)
    {
        //NOTE: DO NOT PROVIDE CREATEDATE! ALSO IT SHOULD BE UTC TIME!
        var comment = GetNewCommentView(parentId); //1 + (int)ContentVariations.AccessByAll);
        comment.module = "NOTALLOWED";
        comment.receiveUserId = 69;

        //THIS SHOULD NOW BE ALLOWED!!
        if(allowed)
        {
            var newModMessage = await writer.WriteAsync(comment, uid);
            Assert.Equal(comment.module, newModMessage.module);
            Assert.Equal(comment.receiveUserId, newModMessage.receiveUserId);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(comment, uid));
        }

        //Now go get some random-ass module messages, but they need to be in NON-DELETED content
        var modMessages = await searcher.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest() {
            type = "message",
            fields = "*",
            query = "!notnull(module) and createUserId = @uid and contentId in @contents"
        }, new Dictionary<string, object>()
        {
            { "uid", uid },
            { "contents", Enumerable.Range(1, fixture.ContentCount).Where(x => ((x - 1) & (int)ContentVariations.Deleted) == 0)}
        });

        Assert.True(modMessages.Count > 0, "No module messages found!");
        
        //Try on 10 random module messages, I don't know
        for(var i = 0; i < 10; i++)
        {
            var modMessage = modMessages[(int)(random.NextInt64() % modMessages.Count)];

            Assert.True(modMessage.id != 0, "Module message pulled didn't have an id set!");

            //Try to edit
            modMessage.text = "EDITED TEXT";

            await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
                await writer.WriteAsync(modMessage, uid);
            });

            //Try to delete 
            await Assert.ThrowsAnyAsync<ForbiddenException>(async () => {
                await writer.DeleteAsync<MessageView>(modMessage.id, uid);
            });
        }
    }

    protected async Task FullPermify(long uid)
    {
        var user = await searcher.GetById<UserView>(RequestType.user, uid);
        var ultraGroup = fixture.UserCount + fixture.GroupCount;

        var relId = await fixture.WriteSingle(new UserRelation() {
            type = UserRelationType.in_group,
            userId = uid,
            relatedId = ultraGroup
        });

        Assert.True(relId > 0, "Couldn't add user to ultra group!");
    }

    /// <summary>
    /// This is a powerful function that enables many tests of field editability for content, regardless of the field type.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="type"></param>
    /// <param name="alterable"></param>
    /// <param name="getField"></param>
    /// <param name="setField"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private async Task WriteAsync_FieldEditable_Generic<T>(long uid, InternalContentType type, bool alterable, Func<ContentView, T> getField, Action<ContentView> setField)
    {
        //To ensure the user can access the content NO MATTER WHAT, set them to the ultra mega group
        await FullPermify(uid);

        //OK, NOW we can go get the content
        var contentId = 1 + (int)type;
        var content = await searcher.GetById<ContentView>(RequestType.content, contentId, true);

        var originalType = getField(content); //content.literalType;
        setField(content);
        //content.literalType = "EDITED_LITERAL";

        //Anyone should be able to write it, BUT
        var written = await writer.WriteAsync(content, uid);

        var writtenField = getField(written);
        var contentField = getField(content);

        //The literalType will be editable based on the "alterable" flag
        if(alterable)
        {
            Assert.Equal(contentField, writtenField);
            Assert.NotEqual(originalType, writtenField);
        }
        else
        {
            Assert.NotEqual(contentField, writtenField);
            Assert.Equal(originalType, writtenField);
        }
    }

    //Nobody should be able to alter the literal type on files, BUT they all should be able to alter 
    //it on content!
    [Theory]
    [InlineData((int)UserVariations.Super, InternalContentType.file, false)]        
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.file, false)]    
    [InlineData((int)UserVariations.Super, InternalContentType.page, true)]         
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.page, true)]     
    [InlineData((int)UserVariations.Super, InternalContentType.module, true)]       
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.module, true)]   
    public Task WriteAsync_LiteralTypeEditable(long uid, InternalContentType type, bool alterable)
    {
        return WriteAsync_FieldEditable_Generic(uid, type, alterable, x => x.literalType, x => x.literalType = "EDITED_LITERAL");
    }

    [Theory]
    [InlineData((int)UserVariations.Super, InternalContentType.file, false)]        
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.file, false)]    
    [InlineData((int)UserVariations.Super, InternalContentType.page, false)]         
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.page, false)]     
    [InlineData((int)UserVariations.Super, InternalContentType.module, false)]       
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.module, false)]   
    public Task WriteAsync_MetaEditable(long uid, InternalContentType type, bool alterable)
    {
        return WriteAsync_FieldEditable_Generic(uid, type, alterable, x => x.meta, x => x.meta = "EDITED_META");
    }

    [Theory]
    [InlineData((int)UserVariations.Super, InternalContentType.file, false)]        
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.file, false)]    
    [InlineData((int)UserVariations.Super, InternalContentType.page, false)]         
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.page, false)]     
    [InlineData((int)UserVariations.Super, InternalContentType.module, false)]       
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.module, false)]   
    public Task WriteAsync_HashEditable(long uid, InternalContentType type, bool alterable)
    {
        return WriteAsync_FieldEditable_Generic(uid, type, alterable, x => x.hash, x => x.hash = "EDITED_HASH");
    }

    //And then just for funsies: make sure the text field is ALWAYS editable
    [Theory]
    [InlineData((int)UserVariations.Super, InternalContentType.file, true)]        
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.file, true)]    
    [InlineData((int)UserVariations.Super, InternalContentType.page, true)]         
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.page, true)]     
    [InlineData((int)UserVariations.Super, InternalContentType.module, true)]       
    [InlineData(1 + (int)UserVariations.Super, InternalContentType.module, true)]   
    public Task WriteAsync_TextEditable(long uid, InternalContentType type, bool alterable)
    {
        return WriteAsync_FieldEditable_Generic(uid, type, alterable, x => x.text, x => x.text = "OMG SUCH A BIG \n EDIT");
    }

    [Theory]
    [InlineData("bad", false)]
    [InlineData("superduperbad", false)]
    [InlineData("fine", true)]
    [InlineData("bad2", false)]
    [InlineData("g-o-o-d", true)]
    [InlineData(" dead", false)]
    public async Task VerifyHash_AllChecks(string hash, bool valid)
    {
        config.HashMinLength = 4;
        config.HashMaxLength = 8;
        config.HashRegex = @"^[a-z\-]+$";

        if(valid)
            await writer.VerifyHash(hash);
        else
            await Assert.ThrowsAnyAsync<ArgumentException>(() => writer.VerifyHash(hash));
    }

    [Fact]
    public async Task VerifyHash_FindDuplicate()
    {
        //Be as LENIENT as possible, we want specifically the DUPLICATE to work
        config.HashMinLength = 1;
        config.HashMaxLength = 800;
        config.HashRegex = ".*";

        var content = await searcher.GetById<ContentView>(RequestType.content, 1 + (int)ContentVariations.AccessByAll, true);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => writer.VerifyHash(content.hash));
    }

    //This is a VERY complex test BUT it serves a very important task, of testing real world
    //scenarios! THIS IS A VERY SLOW TEST!!! Even with only 26 letters, we can somehow make it take ages.
    [Fact]
    public async Task GenerateHash_Gamut()
    {
        //All that REALLY matters is the hash length for auto generation. Well oops, and the retry count.
        //If we somehow reach 100 retries WITHOUT finding 1 out of 5 letters, that's really dumb. BUT,
        //it's definitely a possibility!
        config.AutoHashChars = 1;
        config.AutoHashMaxRetries = 200;

        //This is VERY important, as it makes this test take WAY less time!!
        rng.AlphaSequenceAvailableAlphabet = 8;

        var uid = 1 + (int)UserVariations.Super;

        //We want to write as many content as there are single letter combinations, they should ALL succeed AND have different hashes!
        var hashSet = new HashSet<string>();

        for(var i = 0; i < rng.AlphaSequenceAvailableAlphabet; i++)
        {
            var page = GetNewFileView(); //only files have random hashes now
            var written = await writer.WriteAsync(page, uid);
            Assert.Single(written.hash);
            hashSet.Add(written.hash);
            Assert.Equal(i + 1, hashSet.Count);
        }

        //BUT, attempting to add just ONE more should fail!
        var failPage = GetNewFileView();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => writer.WriteAsync(failPage, uid));
    }

    [Theory]
    [InlineData("this is normal", "this-is-normal", "")]
    [InlineData("isn't this CRAZY&&*(no)", "isnt-this-crazyno", "")]
    [InlineData("nono", null, "")]
    [InlineData("nono", "page-nono", "page")]
    [InlineData("nono", "wbubualfl-nono", "wbu$bu%al(fl)")]
    public async Task GenerateHash_FromName(string name, string? hash, string literalType)
    {
        config.AutoHashChars = 1;
        config.AutoHashMaxRetries = 20;
        config.HashMinLength = 8;

        var page = GetNewPageView(); //only files have random hashes now
        page.literalType = literalType;
        page.name = name;
        var written = await writer.WriteAsync(page, NormalUserId);

        Assert.NotEmpty(written.hash);

        if(hash == null)
        {
            Assert.Equal(1, written.hash.Length);
        }
        else 
        {
            Assert.Equal(hash, written.hash);

            //Write another page with the same name
            var page2 = GetNewPageView();
            page2.literalType = literalType;
            page2.name = name;
            var written2 = await writer.WriteAsync(page2, NormalUserId);
            Assert.Equal($"{hash}1", written2.hash);
        }
    }

    [Theory]
    [InlineData(NormalUserId, 0, false)]
    [InlineData(NormalUserId, 9000, false)]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, 0, false)]
    [InlineData(SuperUserId, 9000, false)]
    [InlineData(SuperUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    public async Task WriteAsync_WatchBasic(long uid, long content, bool allowed)
    {
        //Also test to ensure the watch view auto-sets fields for us
        var watch = new WatchView()
        {
            contentId = content,
            userId = 999, //This should get reset.
        };

        WatchView writtenWatch = watch;

        var writeWatch = new Func<Task>(async () => {
            writtenWatch = await writer.WriteAsync(watch, uid);
        });

        if(allowed)
        {
            await writeWatch();
            Assert.True(writtenWatch.id > 0);
            Assert.Equal(content, writtenWatch.contentId);
            Assert.Equal(uid, writtenWatch.userId);
            AssertWatchEventMatches(writtenWatch, uid, UserAction.create);
        }
        else if(content <= 0 || content >= 1000)
        {
            await Assert.ThrowsAnyAsync<NotFoundException>(writeWatch);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(writeWatch);
        }
    }

    [Theory]
    [InlineData(NormalUserId, 0, false)]
    [InlineData(NormalUserId, 9000, false)]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, 0, false)]
    [InlineData(SuperUserId, 9000, false)]
    [InlineData(SuperUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    public async Task WriteAsync_ContentEngagementBasic(long uid, long content, bool allowed)
    {
        //Also test to ensure the watch view auto-sets fields for us
        var vote = new ContentEngagementView()
        {
            contentId = content,
            userId = 999, //This should get reset.
            type = DbUnitTestSearchFixture.VoteEngagement,
            engagement = "ok"
        };

        ContentEngagementView writtenVote = vote;

        var writeVote = new Func<Task>(async () => {
            writtenVote = await writer.WriteAsync(vote, uid);
        });

        if(allowed)
        {
            await writeVote();
            Assert.True(writtenVote.id > 0);
            Assert.Equal(content, writtenVote.contentId);
            Assert.Equal(uid, writtenVote.userId);
            Assert.Empty(events.Events); //There should be NO events for votes right now!
            //AssertWatchEventMatches(writtenWatch, uid, UserAction.create);
        }
        else if(content <= 0 || content >= 1000)
        {
            await Assert.ThrowsAnyAsync<NotFoundException>(writeVote);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(writeVote);
        }
    }

    [Theory]
    [InlineData(NormalUserId, 0, false)]
    [InlineData(NormalUserId, 9000, false)]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, 0, false)]
    [InlineData(SuperUserId, 9000, false)]
    [InlineData(SuperUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    public async Task WriteAsync_MessageEngagementBasic(long uid, long content, bool allowed)
    {
        long messageId = 0;

        //Gotta actually WRITE the message, since we're given content
        if(content == AllAccessContentId || content == SuperAccessContentId)
        {
            var message = GetNewCommentView(content);
            message.text = "this is a text";
            var writtenMessage = await writer.WriteAsync(message, SuperUserId);
            messageId = writtenMessage.id;
            events.Events.Clear();
        }
        else
        {
            messageId = content; //use the crappy id they gave us
        }

        //Also test to ensure the watch view auto-sets fields for us
        var vote = new MessageEngagementView()
        {
            messageId = messageId,
            userId = 999, //This should get reset.
            type = DbUnitTestSearchFixture.VoteEngagement,
            engagement = "ok"
        };

        MessageEngagementView writtenVote = vote;

        var writeVote = new Func<Task>(async () => {
            writtenVote = await writer.WriteAsync(vote, uid);
        });

        if(allowed)
        {
            await writeVote();
            Assert.True(writtenVote.id > 0);
            Assert.Equal(content, writtenVote.contentId);
            Assert.Equal(messageId, writtenVote.messageId);
            Assert.Equal(uid, writtenVote.userId);
            Assert.Single(events.Events);
            Assert.Equal(messageId, events.Events.First().refId);
            Assert.Equal(EventType.message_event, events.Events.First().type);
        }
        else if(content <= 0 || content >= 1000)
        {
            await Assert.ThrowsAnyAsync<NotFoundException>(writeVote);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(writeVote);
        }
    }

    [Theory]
    [InlineData(NormalUserId, "junk", "other", true)]
    [InlineData(SuperUserId, "junk", "other", true)] 
    [InlineData(NormalUserId, "junk", "junk", false)]
    [InlineData(SuperUserId, "junk", "junk", false)] 
    public async Task WriteAsync_UserVariableBasic(long uid, string firstKey, string secondKey, bool allowed)
    {
        //Also test to ensure the watch view auto-sets fields for us
        var variable = new UserVariableView()
        {
            userId = 999, //This should get reset.
            key = firstKey,
            value = "whatever" 
        };

        UserVariableView writtenVariable = await writer.WriteAsync(variable, uid);

        Assert.True(writtenVariable.id > 0);
        Assert.Equal(uid, writtenVariable.userId);
        Assert.Equal(firstKey, writtenVariable.key);
        Assert.Equal("whatever", writtenVariable.value);
        AssertEventMatchesBase(writtenVariable.id, UserAction.create, uid, EventType.uservariable_event);

        //Now, try to write the second variable
        var secondVariable = new UserVariableView()
        {
            key = secondKey,
            value = "whatever" //We already test lots of unique values, now I want to see same
        };

        if(allowed)
        {
            writtenVariable = await writer.WriteAsync(secondVariable, uid);
            Assert.Equal(uid, writtenVariable.userId);
            Assert.Equal(secondKey, writtenVariable.key);
            Assert.Equal("whatever", writtenVariable.value);
            AssertEventMatchesBase(writtenVariable.id, UserAction.create, uid, EventType.uservariable_event);
        }
        else
        {
            await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(secondVariable, uid));
        }
    }

    public async Task WriteAsync_ConstrictedUserEdit<T>(Func<T> makeNewItem, Func<T, object> getEditField, Action<T, int> setEditField) where T : class, IIdView, new()
    {
        //Also test to ensure the watch view auto-sets fields for us
        var normalItem = makeNewItem(); 
        var superItem = makeNewItem(); 

        var typeInfo = typeInfoService.GetTypeInfo<T>();
        var requestType = typeInfo.requestType ?? throw new InvalidOperationException("No request type in test!");

        setEditField(normalItem, 0);
        setEditField(normalItem, 1);

        normalItem = await writer.WriteAsync(normalItem, NormalUserId);
        superItem = await writer.WriteAsync(superItem, SuperUserId);

        //Both should be able to edit themselves, but not edit the other's
        setEditField(normalItem, 2);
        setEditField(normalItem, 3);

        var normalItemUpdated = await writer.WriteAsync(normalItem, NormalUserId);
        var superItemUpdated = await writer.WriteAsync(superItem, SuperUserId);

        Assert.Equal(getEditField(normalItem), getEditField(normalItemUpdated));
        Assert.Equal(getEditField(superItem), getEditField(superItemUpdated));
        Assert.Equal(normalItem.id, normalItemUpdated.id);
        Assert.Equal(superItem.id, superItemUpdated.id);

        await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(normalItemUpdated, SuperUserId));
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(superItemUpdated, NormalUserId));

        //Neither should be able to delete the others too
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.DeleteAsync<T>(normalItemUpdated.id, SuperUserId));
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.DeleteAsync<T>(superItemUpdated.id, NormalUserId));

        //But these should be fine
        var normalWatchDeleted = writer.DeleteAsync<T>(normalItemUpdated.id, NormalUserId);
        var superWatchDeleted = writer.DeleteAsync<T>(superItemUpdated.id, SuperUserId);

        //As a nice final test, ensure they're actually deleted
        await Assert.ThrowsAnyAsync<NotFoundException>(() => searcher.GetById<T>(requestType, normalItemUpdated.id, true));
        await Assert.ThrowsAnyAsync<NotFoundException>(() => searcher.GetById<T>(requestType, superItemUpdated.id, true));
    }

    [Fact]
    public async Task WriteAsync_SimpleWatchEdit()
    {
        var values = new List<long> { 222, 333, 555, 999 };
        await WriteAsync_ConstrictedUserEdit<WatchView>(
            () => new WatchView() { contentId = AllAccessContentId }, x => x.lastActivityId, (x,i) => x.lastActivityId = values[i]);
    }

    [Fact]
    public async Task WriteAsync_SimpleContentEngagementEdit()
    {
        var values = new List<string> { "ok", "bad", "good", "ok" };
        await WriteAsync_ConstrictedUserEdit<ContentEngagementView>(
            () => new ContentEngagementView() { contentId = AllAccessContentId, type = DbUnitTestSearchFixture.VoteEngagement }, x => x.engagement, (x,i) => x.engagement= values[i]);
    }

    [Fact]
    public async Task WriteAsync_SimpleMessageEngagementEdit()
    {
        var message = GetNewCommentView(AllAccessContentId);
        var writtenMessage = await writer.WriteAsync(message, SuperUserId);
        var values = new List<string> { "ok", "bad", "good", "ok" };
        await WriteAsync_ConstrictedUserEdit<MessageEngagementView>(
            () => new MessageEngagementView() { messageId = writtenMessage.id, type = DbUnitTestSearchFixture.VoteEngagement }, x => x.engagement, (x,i) => x.engagement= values[i]);
    }

    [Fact]
    public async Task WriteAsync_SimpleUserVariableEdit()
    {
        var values = new List<string> { "a", "b", "c", "d" };
        await WriteAsync_ConstrictedUserEdit<UserVariableView>(
            () => new UserVariableView() { key = rng.GetAlphaSequence(5), value = rng.GetAlphaSequence(2) }, 
            x => x.value, (x,i) => x.value = values[i]);
    }

    //A bad bug where permissions would take on whoever was WRITING, very bad!
    [Fact]
    public async Task WriteAsync_Regression_CreateContent_PermissionsSelf()
    {
        //Go create simple content as me
        var content = GetNewPageView();
        content.permissions.Clear();

        var writtenContent = await writer.WriteAsync(content, NormalUserId);

        Assert.Single(writtenContent.permissions);
        Assert.Contains(NormalUserId, writtenContent.permissions.Keys);
    }

    //Ensure that writing as a different person doesn't suddenly make permissions wonky
    [Fact]
    public async Task WriteAsync_Regression_ClearPermissions()
    {
        var content = GetNewPageView();
        content.permissions.Clear();
        content.permissions[0] = "CRUD";

        var writtenContent = await writer.WriteAsync(content, NormalUserId);

        Assert.Contains(0, writtenContent.permissions.Keys);
        Assert.Contains(NormalUserId, writtenContent.permissions.Keys);

        //Now write it again as someone else
        writtenContent.permissions.Clear();

        writtenContent = await writer.WriteAsync(writtenContent, SuperUserId);

        //The only permissions should be the ORIGINAL creator, NOT the super user!
        Assert.Single(writtenContent.permissions);
        Assert.Contains(NormalUserId, writtenContent.permissions.Keys);
    }

    [Fact]
    public async Task WatchView_CantRetrievePrivate()
    {
        //go find a currently not-private page that isn't made by our normal user
        var readableContent = await searcher.SearchSingleType<ContentView>(NormalUserId, new SearchRequest()
        {
            type = "content",
            fields = "*",
            query = "createUserId <> @me"
        }, new Dictionary<string, object> {
            { "me", NormalUserId }
        });

        Assert.NotEmpty(readableContent);

        var content = readableContent.First();
        var watch = new WatchView() { contentId = content.id };
        var writtenWatch = await writer.WriteAsync(watch, NormalUserId);

        var searchWatches = new Func<Task<List<WatchView>>>(() => searcher.SearchSingleType<WatchView>(NormalUserId, new SearchRequest()
        {
            type = "watch",
            fields = "*",
            query = "contentId = @cid"
        }, new Dictionary<string, object> {
            { "cid", content.id }
        }));

        //OK, now go get our watches. There should be ONE
        var watches = await searchWatches();

        Assert.Single(watches);
        Assert.Equal(NormalUserId, watches.First().userId);
        Assert.Equal(content.id, watches.First().contentId);

        //Now, go make that page private. Funny enough, we can still write to it
        //because the original permissions were fine.
        content.permissions.Clear();
        var newContent = await writer.WriteAsync(content, NormalUserId);
        Assert.Single(newContent.permissions); //Should be the create user id
        Assert.Contains(newContent.createUserId, newContent.permissions.Keys);

        //OK, do the previous search again. Should be empty
        watches = await searchWatches();

        Assert.Empty(watches);
    }

    [Fact]
    public async Task WriteAsync_WatchDuplicate()
    {
        //Also test to ensure the watch view auto-sets fields for us
        var watch = new WatchView()
        {
            contentId = AllAccessContentId,
        };

        var writtenWatch = await writer.WriteAsync(watch, NormalUserId);
        Assert.Equal(AllAccessContentId, writtenWatch.contentId);
        Assert.Equal(NormalUserId, writtenWatch.userId);

        //Now write another to the same content
        var watch2 = new WatchView()
        {
            contentId = AllAccessContentId,
        };

        await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(watch2, NormalUserId));

        //BUT we can write it as a super 
        writtenWatch = await writer.WriteAsync(watch2, SuperUserId);
        Assert.Equal(AllAccessContentId, writtenWatch.contentId);
        Assert.Equal(SuperUserId, writtenWatch.userId);
    }

    [Fact] //Watches are TRULY deleted, so we want to make sure it doesn't fail on delete end
    public async Task DeleteAsync_WatchTrueDelete()
    {
        //Also test to ensure the watch view auto-sets fields for us
        var watch = new WatchView()
        {
            contentId = AllAccessContentId,
        };

        var writtenWatch = await writer.WriteAsync(watch, NormalUserId);
        Assert.Equal(AllAccessContentId, writtenWatch.contentId);
        Assert.Equal(NormalUserId, writtenWatch.userId);

        var deletedWatch = await writer.DeleteAsync<WatchView>(writtenWatch.id, NormalUserId);

        //the returned watch NEEDS to display the contentId because it's necessary for frontends and
        //such to like.. work on it 
        Assert.Equal(AllAccessContentId, deletedWatch.contentId);

        await Assert.ThrowsAnyAsync<NotFoundException>(() => searcher.GetById<WatchView>(RequestType.watch, writtenWatch.id));
    }

    [Fact]
    public async Task CommentView_EditedField()
    {
        //Write two comments, both should have edited = false
        var comment1 = GetNewCommentView(AllAccessContentId);
        var comment2 = GetNewCommentView(AllAccessContentId);

        var writtenComment1 = await writer.WriteAsync(comment1, NormalUserId);
        var writtenComment2 = await writer.WriteAsync(comment2, NormalUserId);

        Assert.False(writtenComment1.edited);
        Assert.False(writtenComment2.edited);

        writtenComment1.text = "this is definitely edited!";
        writtenComment1 = await writer.WriteAsync(writtenComment1, NormalUserId);

        Assert.True(writtenComment1.edited);

        var values = new Dictionary<string, object> {
            { "ids", new[] { writtenComment1.id, writtenComment2.id }}
        };

        var search = new SearchRequest()
        {
            type = "message",
            fields = "*",
            query = "id in @ids"
        };

        var comments = await searcher.SearchSingleType<MessageView>(NormalUserId, search, values);

        Assert.True(comments.First(x => x.id == writtenComment1.id).edited);
        Assert.False(comments.First(x => x.id == writtenComment2.id).edited);

        writtenComment2.text = "this is definitely edited 2!";
        writtenComment2 = await writer.WriteAsync(writtenComment2, NormalUserId);

        Assert.True(writtenComment1.edited);

        comments = await searcher.SearchSingleType<MessageView>(NormalUserId, search, values);

        Assert.True(comments.First(x => x.id == writtenComment1.id).edited);
        Assert.True(comments.First(x => x.id == writtenComment2.id).edited);
    }

    [Theory]
    [InlineData(NormalUserId, AllAccessContentId2, true)]
    [InlineData(SuperUserId, AllAccessContentId2, true)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)] //This is the important part
    [InlineData(NormalUserId, 999, false)] //This is the important part
    [InlineData(NormalUserId, 0, false)] //This is the important part
    public async Task WriteAsync_Message_ParentMove(long userId, long newContentId, bool allowed)
    {
        //Write the initial comment
        var comment = await writer.WriteAsync(GetNewCommentView(AllAccessContentId), userId);

        //Change the id
        comment.contentId = newContentId;

        //Try to write it
        if(allowed)
        {
            var result = await writer.WriteAsync(comment, userId);
            AssertDateClose(result.editDate ?? throw new InvalidOperationException("Can not find edit date!!"));
            Assert.Equal(userId, result.editUserId);
            Assert.Equal(newContentId, result.contentId);
        }
        else
        {
            if(newContentId == 0 || newContentId > 900)
                await Assert.ThrowsAnyAsync<NotFoundException>(() => writer.WriteAsync(comment, userId));
            else
                await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(comment, userId));
        }
    }

    [Theory]
    [InlineData(NormalUserId, "system", false)]
    [InlineData(SuperUserId, "system", false)]
    [InlineData(SuperUserId, "evil", false)]
    [InlineData(NormalUserId, "whatever", false)]
    [InlineData(SuperUserId, "whatever", true)]
    public async Task WriteAsync_Module_ReservedName(long userId, string name, bool allowed)
    {
        this.config.ReservedModuleNames = new List<string> { "system", "evil" };

        var module = GetNewModuleView();
        module.name = name;

        var writeModule = new Func<Task<ContentView>>(() => writer.WriteAsync(module, userId));

        //This is create
        if(allowed)
        {
            await writeModule();
            Assert.Equal(name, module.name);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(writeModule);
            //And then fix the name and try again
            module.name = "safe";
            var olduserid = userId;
            userId = SuperUserId;
            await writeModule();
            userId = olduserid;
        }

        //just make sure it's always written
        Assert.NotEqual(0, module.id);

        long id = module.id;
        const string description = "And now for an updated description!";
        module.description = description;
        module.name = name;

        //This is update. Note that regardless of allowed, the above will eventually write the module
        if(allowed)
        {
            await writeModule();
            Assert.Equal(name, module.name);
            Assert.Equal(id, module.id);
        }
        else
        {
            //This should always be an update
            await Assert.ThrowsAnyAsync<ForbiddenException>(writeModule);
        }
    }

    [Fact]
    public async Task WriteAsync_Module_DuplicateName_Disallow()
    {
        var module = GetNewModuleView();
        module.name = "hugs";
        module = await writer.WriteAsync(module, SuperUserId); //this should work
        Assert.NotEqual(0, module.id);

        var module2 = GetNewModuleView();
        module2.name = "hugs";

        await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(module2, SuperUserId));

        //But this SHOULD work
        module.description = "just an update";
        module = await writer.WriteAsync(module, SuperUserId); //this should work
        Assert.Equal("just an update", module.description);
        Assert.NotEqual(0, module.id);
        Assert.Equal("hugs", module.name); //Name should not change
    }

    [Fact]
    public async Task Regression_WriteAsync_ContentId_ParentPerm()
    {
        //Create content with a create only perm for all
        var content = GetNewPageView(0, new Dictionary<long, string> { {0,"C"} });
        content = await writer.WriteAsync(content, SuperUserId);

        //Now post a comment in there
        var comment = GetNewCommentView(content.id);
        comment = await writer.WriteAsync(comment, NormalUserId);

        //Now edit it, it should still let you
        comment.text = "AHAHAHA HEDITKD";
        comment = await writer.WriteAsync(comment, NormalUserId);

        //Now delete it, again should still let you
        var deleted = await writer.DeleteAsync<MessageView>(comment.id, NormalUserId);
        Assert.Equal(comment.id, deleted.id);
        Assert.Equal(comment.contentId, deleted.contentId);
    }

    [Fact]
    public async Task Regression_WriteAsync_ParentId_ParentPerm()
    {
        //Create content with a create only perm for all
        var content = GetNewPageView(0, new Dictionary<long, string> { {0,"C"} });
        content = await writer.WriteAsync(content, SuperUserId);

        //Now post content in there
        var myContent = GetNewPageView(content.id);
        myContent = await writer.WriteAsync(myContent, NormalUserId);

        //Now edit it, it should still let you
        myContent.text = "AHAHAHA HEDITKD";
        myContent = await writer.WriteAsync(myContent, NormalUserId);

        //Now delete it, again should still let you
        var deleted = await writer.DeleteAsync<ContentView>(myContent.id, NormalUserId);
        Assert.Equal(myContent.id, deleted.id);
        Assert.Equal(myContent.parentId, deleted.parentId);
    }

    [Fact]
    public async Task WriteAsync_AdminLog_GroupCreate()
    {
        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group
        };

        //Write the initial group, should always be fine
        var result = await writer.WriteAsync(group, NormalUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id and initiator = @uid"
        }, new Dictionary<string, object>() {
            { "id", result.id },
            { "uid", NormalUserId }
        });

        Assert.NotEmpty(log);
        Assert.Contains(log, x => x.type == AdminLogType.group_create);
    }

    [Fact]
    public async Task WriteAsync_AdminLog_GroupAssign()
    {
        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group,
            usersInGroup = new List<long> { NormalUserId }
        };

        //Write the initial group, should always be fine
        var result = await writer.WriteAsync(group, NormalUserId);

        result.usersInGroup.Add(SuperUserId);

        var updatedResult = await writer.WriteAsync(result, NormalUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id and initiator = @uid"
        }, new Dictionary<string, object>() {
            { "id", result.id },
            { "uid", NormalUserId }
        });

        Assert.NotEmpty(log);
        Assert.Contains(log, x => x.type == AdminLogType.group_create);
        Assert.Contains(log, x => x.type == AdminLogType.group_assign);

        var logItem = log.First(x => x.type == AdminLogType.group_assign);
        Assert.Contains($"'{NormalUserId}'", logItem.text);
        Assert.Contains($"'{NormalUserId},{SuperUserId}'", logItem.text);
    }

    [Fact]
    public async Task WriteAsync_AdminLog_UserDelete()
    {
        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group,
        };

        //Write the initial group, should always be fine
        var result = await writer.WriteAsync(group, NormalUserId);
        var deletedResult = await writer.DeleteAsync<UserView>(result.id, SuperUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id and initiator = @uid"
        }, new Dictionary<string, object>() {
            { "id", result.id },
            { "uid", SuperUserId }
        });

        Assert.NotEmpty(log);
        Assert.DoesNotContain(log, x => x.type == AdminLogType.group_create);
        Assert.Contains(log, x => x.type == AdminLogType.user_delete);

        var logItem = log.First(x => x.type == AdminLogType.user_delete);
        Assert.Contains(group.username, logItem.text);
        Assert.Contains(nameof(UserType.group), logItem.text);
    }

    [Fact]
    public async Task WriteAsync_Username_NoDupes()
    {
        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId);
        user.special = "something_new";
        //Ensure that even though we're updating with the new special, the existing username isn't tripped
        var update1 = await writer.WriteAsync(user, NormalUserId);
        Assert.Equal(user.id, update1.id);
        Assert.Equal("something_new", update1.special);
        //BUT, if we set it to someone else's name, bad
        var other = await searcher.GetById<UserView>(RequestType.user, SuperUserId);
        update1.username = other.username;
        await Assert.ThrowsAnyAsync<ArgumentException>(() => writer.WriteAsync(update1, NormalUserId));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("this_username_is_obviously_way_too_long_I_mean_come_on")]
    [InlineData("$%|baduser")]
    public async Task WriteAsync_Username_ShortLong(string username)
    {
        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId);
        user.special = "something_new";
        //Ensure that even though we're updating with the new special, the existing username isn't tripped
        var update1 = await writer.WriteAsync(user, NormalUserId);
        Assert.Equal(user.id, update1.id);
        Assert.Equal("something_new", update1.special);
        //BUT, if we set it to someone else's name, bad
        update1.username = username;
        await Assert.ThrowsAnyAsync<ArgumentException>(() => writer.WriteAsync(update1, NormalUserId));
    }

    [Theory]
    [InlineData(NormalUserId, SuperUserId, false)]
    [InlineData(NormalUserId, NormalUserId, false)]
    [InlineData(SuperUserId, NormalUserId, true)]
    [InlineData(SuperUserId, SuperUserId, true)] //You can ban yourself I guess
    public async Task WriteAsync_Ban_Allowed(long banner, long bannee, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = BanType.@public,
            bannedUserId = bannee,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        if(allowed)
        {
            var writtenBan = await writer.WriteAsync(ban, banner);
            Assert.True(writtenBan.id > 0);
            Assert.Equal(bannee, writtenBan.bannedUserId);
            Assert.Equal(banner, writtenBan.createUserId);
            AssertDateClose(ban.expireDate, writtenBan.expireDate);
            AssertDateClose(writtenBan.createDate);
        }
        else
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.WriteAsync(ban, banner));
        }
    }

    [Theory]
    [InlineData("CRUD", BanType.none, true)]
    [InlineData("CRUD", BanType.@public, false)]
    [InlineData("CRUD", BanType.@private, true)]
    [InlineData("CRUD", BanType.user, true)]
    [InlineData("", BanType.@public, true)]
    [InlineData("", BanType.@private, false)]
    [InlineData("", BanType.user, true)]
    [InlineData("CRUD", BanType.@public | BanType.@private, false)]
    [InlineData("", BanType.@public | BanType.@private, false)]
    [InlineData("", BanType.@public | BanType.@private | BanType.user, false)]
    public async Task WriteAsync_Ban_AllowedOnContent(string publicperms, BanType type, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = type,
            bannedUserId = NormalUserId,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        var content = GetNewPageView(0);
        content.permissions[0] = publicperms;
        content.permissions[NormalUserId] = "CRUD"; //full perms for our banned user
        content.keywords.Add("WOWEEZOWEE");

        var writtenBan = await writer.WriteAsync(ban, SuperUserId);

        if(allowed)
        {
            var finalContent = await writer.WriteAsync(content, NormalUserId);
            Assert.Contains("WOWEEZOWEE", finalContent.keywords);
        }
        else
        {
            await Assert.ThrowsAnyAsync<BannedException>(() => writer.WriteAsync(content, NormalUserId));
        }
    }

    [Theory]
    [InlineData("CRUD", BanType.none, true)]
    [InlineData("CRUD", BanType.@public, false)]
    [InlineData("CRUD", BanType.@private, true)]
    [InlineData("CRUD", BanType.user, true)]
    [InlineData("", BanType.@public, true)]
    [InlineData("", BanType.@private, false)]
    [InlineData("", BanType.user, true)]
    [InlineData("CRUD", BanType.@public | BanType.@private, false)]
    [InlineData("", BanType.@public | BanType.@private, false)]
    [InlineData("", BanType.@public | BanType.@private | BanType.user, false)]
    public async Task WriteAsync_Ban_AllowedOnContent_Edit(string publicperms, BanType type, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = type,
            bannedUserId = NormalUserId,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        var content = await searcher.GetById<ContentView>(RequestType.content, AllAccessContentId);
        content.permissions[0] = publicperms;
        content.permissions[NormalUserId] = "CRUD"; //full perms for our banned user
        var writtenContent = await writer.WriteAsync(content, NormalUserId); //This ensures the user has normal access to it

        var writtenBan = await writer.WriteAsync(ban, SuperUserId);

        writtenContent.keywords.Add("WOWEEZOWEE");

        if(allowed)
        {
            var finalContent = await writer.WriteAsync(writtenContent, NormalUserId);
            Assert.Contains("WOWEEZOWEE", finalContent.keywords);
        }
        else
        {
            await Assert.ThrowsAnyAsync<BannedException>(() => writer.WriteAsync(writtenContent, NormalUserId));
        }
    }

    [Theory]
    [InlineData(BanType.none, NormalUserId, NormalUserId, true)]
    [InlineData(BanType.@public, NormalUserId, NormalUserId, true)]
    [InlineData(BanType.@private, NormalUserId, NormalUserId, true)]
    [InlineData(BanType.@public | BanType.@private, NormalUserId, NormalUserId, true)]
    [InlineData(BanType.user, NormalUserId, NormalUserId, false)]
    [InlineData(BanType.user | BanType.@public | BanType.@private, NormalUserId, NormalUserId, false)]
    [InlineData(BanType.user, SuperUserId, NormalUserId, false)]
    [InlineData(BanType.user, SuperUserId, SuperUserId, false)]
    public async Task WriteAsync_Ban_UserEdits(BanType bantype, long banUserId, long modifyUserId, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = bantype,
            bannedUserId = banUserId,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        var writtenBan = await writer.WriteAsync(ban, SuperUserId);

        var user = await searcher.GetById<UserView>(RequestType.user, modifyUserId);
        user.special = "THIS IS A BAN OR SOMETHING!!";

        if(allowed)
        {
            var finalUser = await writer.WriteAsync(user, banUserId);
            Assert.Equal(user.special, finalUser.special);
        }
        else
        {
            await Assert.ThrowsAnyAsync<BannedException>(() => writer.WriteAsync(user, banUserId));
        }
    }

    [Theory]
    [InlineData(BanType.none, NormalUserId, true)]
    [InlineData(BanType.@public, NormalUserId, true)]
    [InlineData(BanType.@private, NormalUserId, true)]
    [InlineData(BanType.@public | BanType.@private, NormalUserId, true)]
    [InlineData(BanType.none, SuperUserId, true)]
    [InlineData(BanType.@public, SuperUserId, true)]
    [InlineData(BanType.@private, SuperUserId, true)]
    [InlineData(BanType.@public | BanType.@private, SuperUserId, true)]
    [InlineData(BanType.user, NormalUserId, false)]
    [InlineData(BanType.user | BanType.@public | BanType.@private, NormalUserId, false)]
    [InlineData(BanType.user, SuperUserId, false)]
    [InlineData(BanType.user | BanType.@public | BanType.@private, SuperUserId, false)]
    public async Task WriteAsync_Ban_NewUser(BanType bantype, long banUserId, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = bantype,
            bannedUserId = banUserId,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        var writtenBan = await writer.WriteAsync(ban, SuperUserId);

        var group = new UserView()
        {
            username = "some_group",
            type = UserType.group
        };

        if(allowed)
        {
            var finalGroup = await writer.WriteAsync(group, banUserId);
            Assert.Equal(group.username, finalGroup.username);
        }
        else
        {
            await Assert.ThrowsAnyAsync<BannedException>(() => writer.WriteAsync(group, banUserId));
        }
    }

    [Theory]
    [InlineData("CRUD", BanType.none, true)]
    [InlineData("CRUD", BanType.@public, false)]
    [InlineData("CRUD", BanType.@private, true)]
    [InlineData("", BanType.@public, true)]
    [InlineData("", BanType.@private, false)]
    [InlineData("CRUD", BanType.@public | BanType.@private, false)]
    [InlineData("", BanType.@public | BanType.@private, false)]
    public async Task WriteAsync_Ban_AllowedOnMessage(string publicperms, BanType type, bool allowed)
    {
        //This should be all you need
        var ban = new BanView()
        {
            type = type,
            bannedUserId = NormalUserId,
            message = "You are banned",
            expireDate = DateTime.UtcNow.AddDays(1)
        };

        var content = await searcher.GetById<ContentView>(RequestType.content, AllAccessContentId);
        content.permissions[0] = publicperms;
        content.permissions[NormalUserId] = "CRUD"; //full perms for our banned user
        var writtenContent = await writer.WriteAsync(content, NormalUserId);
        
        //Make sure it works before the ban
        var message = GetNewCommentView(AllAccessContentId);
        var writtenMessage = await writer.WriteAsync(message, NormalUserId);
        Assert.NotEqual(0, writtenMessage.id);

        await writer.WriteAsync(ban, SuperUserId);

        message = GetNewCommentView(AllAccessContentId);
        message.text = "WOWEEZOWEE";

        if(allowed)
        {
            writtenMessage = await writer.WriteAsync(message, NormalUserId);
            Assert.Contains("WOWEEZOWEE", message.text);
        }
        else
        {
            await Assert.ThrowsAnyAsync<BannedException>(() => writer.WriteAsync(writtenContent, NormalUserId));
        }
    }

    [Fact]
    public async Task RestoreContent_NotFound()
    {
        await Assert.ThrowsAnyAsync<NotFoundException>(() => writer.RestoreContent(99999, NormalUserId));
    }

    [Fact]
    public async Task RestoreContent_DisallowSameRevision()
    {
        var content = await searcher.GetById<ContentView>(AllAccessContentId, true);
        await Assert.ThrowsAnyAsync<RequestException>(() => writer.RestoreContent(content.lastRevisionId, NormalUserId));
    }

    [Fact]
    public async Task RestoreContent_DisallowForbiddenContent()
    {
        var content = await searcher.GetById<ContentView>(SuperAccessContentId, true);
        var oldRevision = content.lastRevisionId;
        content.text = "haha something new";
        var newRevision = await writer.WriteAsync(content, SuperUserId);
        await Assert.ThrowsAnyAsync<ForbiddenException>(() => writer.RestoreContent(oldRevision, NormalUserId));
    }

    [Fact]
    public async Task RestoreContent_General()
    {
        var content = await searcher.GetById<ContentView>(AllAccessContentId, true);
        //Generate a new revisionID since the existing is bogus, ugh
        content = await writer.WriteAsync(content, SuperUserId);
        var oldRevision = content.lastRevisionId;
        var oldText = content.text;
        var oldValues = content.values;
        var oldKeywords = content.keywords;
        var oldPermissions = content.permissions;
        content.text = "haha something new";
        content.values = new Dictionary<string, object> { {"abc", 123}};
        content.keywords = new List<string> { "ugh", "ugh2", "help"};
        content.permissions = new Dictionary<long, string> { { NormalUserId, "CRUD"} };
        var newContent = await writer.WriteAsync(content, SuperUserId);
        Assert.NotEqual(oldRevision, newContent.lastRevisionId);
        Assert.NotEqual(oldText, newContent.text);
        Assert.False(content.keywords.OrderBy(x => x).SequenceEqual(oldKeywords.OrderBy(x => x)));
        Assert.False(content.values.Keys.OrderBy(x => x).SequenceEqual(oldValues.Keys.OrderBy(x => x)));
        Assert.False(content.permissions.Keys.OrderBy(x => x).SequenceEqual(oldPermissions.Keys.OrderBy(x => x)));
        var resultContent = await writer.RestoreContent(oldRevision, NormalUserId);
        Assert.NotEqual(oldRevision, resultContent.lastRevisionId);
        Assert.NotEqual(newContent.text, resultContent.text);
        Assert.False(newContent.keywords.OrderBy(x => x).SequenceEqual(resultContent.keywords.OrderBy(x => x)));
        Assert.False(newContent.values.Keys.OrderBy(x => x).SequenceEqual(resultContent.values.Keys.OrderBy(x => x)));
        Assert.False(newContent.permissions.Keys.OrderBy(x => x).SequenceEqual(resultContent.permissions.Keys.OrderBy(x => x)));
        Assert.Equal(oldText, resultContent.text);
        Assert.True(resultContent.keywords.OrderBy(x => x).SequenceEqual(oldKeywords.OrderBy(x => x)));
        Assert.True(resultContent.values.Keys.OrderBy(x => x).SequenceEqual(oldValues.Keys.OrderBy(x => x)));
        Assert.True(resultContent.permissions.Keys.OrderBy(x => x).SequenceEqual(oldPermissions.Keys.OrderBy(x => x)));
    }

    [Theory]
    [InlineData("heck", true)]
    [InlineData("hecking%*490%$*)%", true)]
    [InlineData("'=-_+0)9(8*7&6^5%4$3#2@1!wow`~\\][}{;:,<.>/?", true)]
    [InlineData("💙loveu", true)]
    [InlineData("no way", false)]
    [InlineData("no\"way", false)]
    [InlineData(" noway", false)]
    [InlineData("noway ", false)]
    [InlineData("\"noway", false)]
    [InlineData("noway\"", false)]
    public async Task Write_KeywordCheck(string keyword, bool allowed)
    {
        var content = GetNewPageView();
        content.keywords.Add(keyword);

        if(allowed)
        {
            var written = await writer.WriteAsync(content, NormalUserId);
            Assert.Contains(keyword, written.keywords);
        }
        else
        {
            await Assert.ThrowsAnyAsync<RequestException>(() => writer.WriteAsync(content, NormalUserId));
        }

    }

    [Theory]
    [InlineData(SuperUserId, NormalUserId, AllAccessContentId, 0)]
    [InlineData(SuperUserId, NormalUserId, AllAccessContentId2, 0)]
    [InlineData(SuperUserId, SuperUserId, SuperAccessContentId, 0)]
    [InlineData(SuperUserId, 9999, AllAccessContentId, 1)]
    [InlineData(SuperUserId, NormalUserId, 9999, 1)]
    [InlineData(NormalUserId, NormalUserId, AllAccessContentId, 2)]
    public async Task Write_UserRelation_AssignContent(long writerId, long userId, long relatedId, int failType)
    {
        var relation = new UserRelationView {
            type = UserRelationType.assign_content,
            userId = userId,
            relatedId = relatedId
        };

        var writing = new Func<Task<UserRelationView>>(() => writer.WriteAsync(relation,writerId));

        if(failType == 0)
        {
            var result = await writing();
            Assert.True(result.id > 0);
            Assert.Equal(userId, result.userId);
            Assert.Equal(relatedId, result.relatedId);
            Assert.Equal(UserRelationType.assign_content, result.type);
            AssertDateClose(result.createDate);

            //And you know what, I'm tired, just test this too
            var deleteResult = await writer.DeleteAsync<UserRelationView>(result.id, writerId);
            Assert.Equal(result.id, deleteResult.id);
            Assert.Equal(userId, deleteResult.userId);
            Assert.Equal(relatedId, deleteResult.relatedId);
            Assert.Equal(UserRelationType.assign_content, deleteResult.type);

            //And go look it up too just because
            var lookup_relation = await searcher.SearchSingleType<UserRelationView>(writerId, new SearchRequest()
            {
                type = nameof(RequestType.userrelation),
                fields = "*",
                query = "id = @id"
            }, new Dictionary<string, object> { {"id", result.id}});

            Assert.Empty(lookup_relation);
        }
        else if(failType == 1)
        {
            await Assert.ThrowsAnyAsync<NotFoundException>(writing);
        }
        else if (failType == 2)
        {
            await Assert.ThrowsAnyAsync<ForbiddenException>(writing);
        }
    }

    //private async Task<AdminLogView> GetLastAdminLog()
    //{
    //    var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
    //        type = nameof(RequestType.adminlog),
    //        fields = "*",
    //        query = "",
    //        limit = 1,
    //        order = "id_desc"
    //    }, new Dictionary<string, object>() { });
    //}

    [Fact]
    public async Task WriteAsync_NewMessage_NoAdminLog()
    {
        var message = GetNewCommentView(AllAccessContentId);
        var result = await writer.WriteAsync(message, NormalUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id"
        }, new Dictionary<string, object>() { { "id", result.id }, });

        Assert.Empty(log);
    }

    [Fact]
    public async Task WriteAsync_EditMessage_AdminLog()
    {
        var message = GetNewCommentView(AllAccessContentId);
        var result = await writer.WriteAsync(message, NormalUserId);
        result.text = "WOWEEXZONGHE";
        var result2 = await writer.WriteAsync(result, NormalUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id"
        }, new Dictionary<string, object>() { { "id", result.id }, });

        Assert.Single(log);
        var item = log.First();
        Assert.Equal(NormalUserId, item.initiator);
        Assert.Equal(AdminLogType.message_edit, item.type);
    }

    [Fact]
    public async Task WriteAsync_DeleteMessage_AdminLog()
    {
        var message = GetNewCommentView(AllAccessContentId);
        var result = await writer.WriteAsync(message, NormalUserId);
        var result2 = await writer.DeleteAsync<MessageView>(result.id, NormalUserId);

        var log = await searcher.SearchSingleTypeUnrestricted<AdminLogView>(new SearchRequest() {
            type = nameof(RequestType.adminlog),
            fields = "*",
            query = "target = @id"
        }, new Dictionary<string, object>() { { "id", result.id }, });

        Assert.Single(log);
        var item = log.First();
        Assert.Equal(NormalUserId, item.initiator);
        Assert.Equal(AdminLogType.message_delete, item.type);
    }
}