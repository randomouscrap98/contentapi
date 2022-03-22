using System.Threading.Tasks;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class ShortcutsServiceTests : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbUnitTestSearchFixture fixture;
    protected ShortcutsService service;
    protected IDbWriter writer;
    protected IGenericSearch search;

    public ShortcutsServiceTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.writer = fixture.GetService<IDbWriter>();
        this.search = fixture.GetService<IGenericSearch>();
        this.service = new ShortcutsService(fixture.GetService<ILogger<ShortcutsService>>(), writer, search);
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

        var vote =  new VoteView() { contentId = cid, vote = Db.VoteType.ok };
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
}