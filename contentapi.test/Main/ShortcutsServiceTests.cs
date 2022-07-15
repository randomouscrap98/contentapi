using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.data.Views;
using Microsoft.Extensions.Logging;
using Xunit;
using contentapi.data;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class ShortcutsServiceTests : ViewUnitTestBase 
{
    protected DbUnitTestSearchFixture fixture;
    protected ShortcutsService service;
    protected IDbWriter writer;
    protected IGenericSearch search;
    protected IMapper mapper;

    public ShortcutsServiceTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.writer = fixture.GetWriter();
        this.search = fixture.GetGenericSearcher();
        this.mapper = fixture.GetService<IMapper>();
        this.service = new ShortcutsService(fixture.GetService<ILogger<ShortcutsService>>(), fixture.dbFactory, mapper);
    }

    [Theory]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, AllAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    public async Task ClearNotifications_Simple(long uid, long cid, bool allowed)
    {
        var watch = new WatchView() { contentId = cid };
        
        //Clearing the notifications is a simple lookup, it does NOT throw exceptions
        //when the content wasn't found.
        await service.ClearNotificationsAsync(watch, uid);

        if(allowed)
        {
            Assert.True(watch.lastActivityId > 0);
            Assert.True(watch.lastCommentId > 0);
        }
        else
        {
            Assert.Equal(0, watch.lastActivityId);
            Assert.Equal(0, watch.lastCommentId);
        }
    }

    [Theory]
    [InlineData(NormalUserId, AllAccessContentId)]
    [InlineData(SuperUserId, AllAccessContentId)]
    public async Task LookupWatchByContentId_Simple(long uid, long cid)
    {
        //Shouldn't exist at first... we hope?
        await Assert.ThrowsAnyAsync<NotFoundException>(() => service.LookupWatchByContentIdAsync(uid, cid));

        var watch =  new WatchView() { contentId = cid };
        await service.ClearNotificationsAsync(watch, uid);
        var writtenWatch = await writer.WriteAsync(watch, uid);

        Assert.Equal(watch.lastCommentId, writtenWatch.lastCommentId);
        Assert.Equal(watch.lastActivityId, writtenWatch.lastActivityId);

        //now go look it up
        var lookupWatch = await service.LookupWatchByContentIdAsync(uid, cid);

        Assert.Equal(uid, lookupWatch.userId);
        Assert.Equal(cid, lookupWatch.contentId);
        Assert.Equal(watch.lastCommentId, lookupWatch.lastCommentId);
        Assert.Equal(watch.lastActivityId, lookupWatch.lastActivityId);
    }

    [Theory]
    [InlineData(NormalUserId, AllAccessContentId)]
    [InlineData(SuperUserId, AllAccessContentId)]
    public async Task LookupVoteByContentId_Simple(long uid, long cid)
    {
        //Shouldn't exist at first... we hope?
        await Assert.ThrowsAnyAsync<NotFoundException>(() => service.LookupVoteByContentIdAsync(uid, cid));

        var vote =  new VoteView() { contentId = cid, vote = VoteType.ok };
        var writtenVote = await writer.WriteAsync(vote, uid);

        Assert.Equal(vote.vote, writtenVote.vote);
        Assert.Equal(vote.contentId, writtenVote.contentId);

        //now go look it up
        var lookupVote = await service.LookupVoteByContentIdAsync(uid, cid);

        Assert.Equal(uid, lookupVote.userId);
        Assert.Equal(cid, lookupVote.contentId);
        Assert.Equal(vote.vote, lookupVote.vote);
    }

    [Theory]
    [InlineData(NormalUserId, "whateverkey")]
    [InlineData(SuperUserId, "whateverkey")]
    public async Task LookupVariableByKey_Simple(long uid, string key)
    {
        //Shouldn't exist at first... we hope?
        await Assert.ThrowsAnyAsync<NotFoundException>(() => service.LookupVariableByKeyAsync(uid, key));

        var variable = new UserVariableView() { key = key, value = "heck" };
        var writtenVariable = await writer.WriteAsync(variable, uid);

        Assert.Equal(variable.value, writtenVariable.value);
        Assert.Equal(variable.key, writtenVariable.key);

        //now go look it up
        var lookupVariable = await service.LookupVariableByKeyAsync(uid, key);

        Assert.Equal(uid, lookupVariable.userId);
        Assert.Equal(key, lookupVariable.key);
        Assert.Equal(writtenVariable.value, lookupVariable.value);
    }

    [Theory]
    [InlineData(AllAccessContentId, AllAccessContentId, NormalUserId, NormalUserId, NormalUserId, AllAccessContentId2, true)]  //Just a basic "move my comments"
    [InlineData(SuperAccessContentId, SuperAccessContentId, SuperUserId, SuperUserId, SuperUserId, AllAccessContentId2, true)] 
    [InlineData(AllAccessContentId, SuperAccessContentId, NormalUserId, SuperUserId, NormalUserId, AllAccessContentId2, false)] //because some are super
    [InlineData(SuperAccessContentId, SuperAccessContentId, SuperUserId, SuperUserId, NormalUserId, AllAccessContentId2, false)] //because ALL are super
    [InlineData(AllAccessContentId, AllAccessContentId, SuperUserId, SuperUserId, NormalUserId, AllAccessContentId2, false)] //because ALL are super (writer)
    [InlineData(AllAccessContentId, SuperAccessContentId, NormalUserId, SuperUserId, SuperUserId, AllAccessContentId2, true)] //Supers allowed
    //NOTE: PROBABLY THE MOST IMPORTANT TEST IN THIS SUITE! This should be another test in dbwriter!
    [InlineData(AllAccessContentId, AllAccessContentId, NormalUserId, NormalUserId, NormalUserId, SuperAccessContentId, false)]  //Even if you're moving your comments, can't go into bad parent
    [InlineData(AllAccessContentId, AllAccessContentId, NormalUserId, NormalUserId, NormalUserId, 0, false)]                     
    [InlineData(SuperAccessContentId, SuperAccessContentId, SuperUserId, SuperUserId, SuperUserId, 0, false)]
    public async Task RethreadMissing(long content1, long content2, long write1, long write2, long rethreadUser, long rethreadPlace, bool allowed)
    {
        const int commentsCount = 5;

        var writtenMessages = new List<MessageView>();

        //Add a bunch of comments to the two contents (might be the same)
        for(var i = 0; i < commentsCount; i++)
        {
            var ncomment = GetNewCommentView(content1);
            writtenMessages.Add(await writer.WriteAsync(ncomment, write1));
            var scomment = GetNewCommentView(content2);
            writtenMessages.Add(await writer.WriteAsync(scomment, write2));
        }

        Assert.Equal(commentsCount * 2, writtenMessages.Count);

        var work = new Func<Task<List<MessageView>>>(() => service.RethreadMessagesAsync(writtenMessages.Select(x => x.id).ToList(), rethreadPlace, rethreadUser));

        //Now, try to rethread
        if(allowed)
        {
            var result = await work();
            Assert.All(result, x => 
            {
                AssertDateClose(x.editDate ?? throw new InvalidOperationException("NO EDIT DATE SET"));
                Assert.Equal(rethreadPlace, x.contentId);
                Assert.Contains(writtenMessages, y => y.id == x.id);
                Assert.Equal(rethreadUser, x.editUserId);
            });
        }
        else
        {
            //This could be permissions or it could be request or anything... this is kind of dangerous but whatever
            await Assert.ThrowsAnyAsync<Exception>(work);

            //Go search these messages
            var currentMessages = await search.SearchSingleTypeUnrestricted<MessageView>(new SearchRequest()
            {
                type = "message",
                fields = "*",
                query = "id in @ids"
            }, new Dictionary<string, object>()
            {
                { "ids", writtenMessages.Select(x => x.id )}
            });

            Assert.All(currentMessages, x => 
            {
                Assert.Null(x.editDate);
                Assert.NotEqual(rethreadPlace, x.contentId);
                Assert.NotEqual(rethreadUser, x.editUserId);
            });
        }
    }
}