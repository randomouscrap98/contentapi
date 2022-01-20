using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class EventQueueTest : UnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected EventQueue queue;
    protected ICacheCheckpointTracker<EventData> tracker;
    protected IPermissionService permission;
    protected EventQueueConfig config;

    //The tests here are rather complicated; we can probably simplify them in the future, but for now,
    //I just need a system that REALLY tests if this whole thing works, and that is most reliable if 
    //I just use the (known to work) dbwriter to set up the database in a way we expect.
    public EventQueueTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
        this.tracker = fixture.GetService<ICacheCheckpointTracker<EventData>>();
        this.searcher = fixture.GetService<IGenericSearch>();
        this.permission = fixture.GetService<IPermissionService>();
        this.config = new EventQueueConfig();
        this.queue = new EventQueue(fixture.GetService<ILogger<EventQueue>>(), this.config, this.tracker, () => this.searcher, this.permission, this.mapper);
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), this.searcher, 
            fixture.GetService<Db.ContentApiDbConnection>(), fixture.GetService<ITypeInfoService>(), this.mapper,
            fixture.GetService<Db.History.IHistoryConverter>(), this.permission, this.queue); 

        //Reset it for every test
        fixture.ResetDatabase();
    }

    [Fact]
    public void GetSearchRequestForEvents_RunsWithoutException()
    {
        //Can we at LEAST run it without error? There should be a user and content by id 1, and content 1 is created by user 1
        var request = queue.GetSearchRequestsForEvents(new [] { new EventData(1, Db.UserAction.create, EventType.activity, 1) });
        Assert.NotNull(request);
        Assert.NotEmpty(request.requests);
        Assert.NotEmpty(request.values);
    }

    //First, without actually doing anything with the event part, ensure the core of the service works. Does
    //building a request for events create something we expect?
    [Fact]
    public async Task GetSearchRequestForEvents_Activity()
    {
        //Go get all activity for content 1
        var search = new SearchRequests();
        search.values.Add("id", 1);
        search.requests.Add(new SearchRequest()
        {
            type = "activity",
            fields = "*",
            query = "contentId = @id"
        });
        var baseResult = await searcher.SearchUnrestricted(search);
        var activities = searcher.ToStronglyTyped<ActivityView>(baseResult.data["activity"]);

        Assert.True(activities.Count > 1); //It should be greater than 1 for content 1, because of inverse activity amounts
        foreach(var a in activities)
        {
            //The event user shouldn't matter but just in case...
            var request = queue.GetSearchRequestsForEvents(new[] { new EventData(a.userId, Db.UserAction.create, EventType.activity, a.id) });
            var result = await searcher.SearchUnrestricted(request);

            //Now, make sure the result contains content, activity, and user results.
            Assert.True(result.data.ContainsKey("content"));
            Assert.True(result.data.ContainsKey("activity"));
            Assert.True(result.data.ContainsKey("user"));

            var content = searcher.ToStronglyTyped<ContentView>(result.data["content"]);
            var activity = searcher.ToStronglyTyped<ActivityView>(result.data["activity"]);
            var user = searcher.ToStronglyTyped<UserView>(result.data["user"]);

            Assert.Single(content);
            Assert.Single(activity);
            Assert.Single(user);

            Assert.Equal(1, user.First().id);
            Assert.Equal(1, content.First().id); //ALl activity should still point to content 1
            Assert.Equal(1, activity.First().contentId);
            Assert.Equal(a.id, activity.First().id);
        }
    }

    [Fact]
    public async Task GetSearchRequestForEvents_Comment()
    {
        //Go get all activity for content 1
        var search = new SearchRequests();
        var contentId = 1 + (int)ContentVariations.Comments;
        search.values.Add("id", contentId);
        search.requests.Add(new SearchRequest()
        {
            type = "comment",
            fields = "*",
            query = "contentId = @id"
        });
        var baseResult = await searcher.SearchUnrestricted(search);
        var comments= searcher.ToStronglyTyped<CommentView>(baseResult.data["comment"]);

        Assert.True(comments.Count > 1); //It should be greater than 1 for content 1, because of inverse activity amounts
        foreach(var c in comments)
        {
            //The event user shouldn't matter but just in case...
            var request = queue.GetSearchRequestsForEvents(new[] { new EventData(c.createUserId, Db.UserAction.create, EventType.comment, c.id) });
            var result = await searcher.SearchUnrestricted(request);

            //Now, make sure the result contains content, comment, and user results.
            Assert.True(result.data.ContainsKey("content"));
            Assert.True(result.data.ContainsKey("comment"));
            Assert.True(result.data.ContainsKey("user"));

            var content = searcher.ToStronglyTyped<ContentView>(result.data["content"]);
            var comment = searcher.ToStronglyTyped<CommentView>(result.data["comment"]);
            var user = searcher.ToStronglyTyped<UserView>(result.data["user"]);

            Assert.Single(content);
            Assert.Single(comment);
            Assert.NotEmpty(user); //don't know how many users there will be, depends on factors

            //Make sure the content creator AND the comment user are in the user list
            Assert.True(user.Any(x => x.id == content.First().createUserId), $"Couldn't find the create user for content {contentId} ({content.First().createUserId})"); 
            Assert.True(user.Any(x => x.id == c.createUserId), $"Couldn't find the comment user for comment {c.id} ({c.createUserId})"); 
            Assert.Equal(contentId, content.First().id);
            Assert.Equal(contentId, comment.First().contentId);
            Assert.Equal(c.id, comment.First().id);
        }
    }
}