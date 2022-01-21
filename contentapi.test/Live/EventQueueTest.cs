using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Xunit;
using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.test;

public class EventQueueTest : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected EventQueue queue;
    protected ICacheCheckpointTracker<LiveEvent> tracker;
    protected IPermissionService permission;
    protected EventQueueConfig config;
    protected CacheCheckpointTrackerConfig trackerConfig;

    //The tests here are rather complicated; we can probably simplify them in the future, but for now,
    //I just need a system that REALLY tests if this whole thing works, and that is most reliable if 
    //I just use the (known to work) dbwriter to set up the database in a way we expect.
    public EventQueueTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();
        this.searcher = fixture.GetService<IGenericSearch>();
        this.permission = fixture.GetService<IPermissionService>();
        this.config = new EventQueueConfig()
        {
            //Ensure nothing ever expires
            DataCacheExpire = System.TimeSpan.MaxValue
        };
        this.trackerConfig = new CacheCheckpointTrackerConfig()
        {
            CacheCleanFrequency = int.MaxValue
        };
        //Note: WE HAVE TO create a new tracker every time! We don't want old data clogging this up!!
        this.tracker = new CacheCheckpointTracker<LiveEvent>(fixture.GetService<ILogger<CacheCheckpointTracker<LiveEvent>>>(), trackerConfig);
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
        var request = queue.GetSearchRequestsForEvents(new [] { new LiveEvent(1, Db.UserAction.create, EventType.activity, 1) });
        Assert.NotNull(request);
        Assert.NotEmpty(request.requests);
        Assert.NotEmpty(request.values);
    }

    private async Task<List<ActivityView>> GetActivityForContentAsync(long id)
    {
        //Go get all activity for content 1
        var search = new SearchRequests();
        search.values.Add("id", id);
        search.requests.Add(new SearchRequest()
        {
            type = "activity",
            fields = "*",
            query = "contentId = @id"
        });
        var baseResult = await searcher.SearchUnrestricted(search);
        var activities = searcher.ToStronglyTyped<ActivityView>(baseResult.data["activity"]);
        return activities;
    }

    private void AssertSimpleActivityListenResult(Dictionary<string, QueryResultSet> data, ContentView content, ActivityView activity)
    {
        //Now, make sure the result contains content, activity, and user results.
        Assert.True(data.ContainsKey("content"));
        Assert.True(data.ContainsKey("activity"));
        Assert.True(data.ContainsKey("user"));

        //The content, when requested, MUST have permissions!!
        Assert.True(data["content"].First().ContainsKey("permissions"));

        var contents = searcher.ToStronglyTyped<ContentView>(data["content"]);
        var activities = searcher.ToStronglyTyped<ActivityView>(data["activity"]);
        var users = searcher.ToStronglyTyped<UserView>(data["user"]);

        Assert.Single(contents);
        Assert.Single(activities);
        Assert.NotEmpty(users); //don't know how many users there will be, depends on factors

        Assert.True(users.Any(x => x.id == contents.First().createUserId), $"Couldn't find the create user for content {content.id} ({contents.First().createUserId})"); 
        Assert.True(users.Any(x => x.id == activity.userId), $"Couldn't find the activity user for activity {activity.id} ({activity.userId})"); 
        Assert.Equal(content.id, contents.First().id); 
        Assert.Equal(content.id, activities.First().contentId);
        Assert.Equal(activity.id, activities.First().id);
    }

    //First, without actually doing anything with the event part, ensure the core of the service works. Does
    //building a request for events create something we expect?
    [Fact]
    public async Task GetSearchRequestForEvents_Activity()
    {
        var content = await searcher.GetById<ContentView>(RequestType.content, 1);
        var activities = await GetActivityForContentAsync(1);
        Assert.True(activities.Count > 1); //It should be greater than 1 for content 1, because of inverse activity amounts

        foreach(var a in activities)
        {
            //The event user shouldn't matter but just in case...
            var request = queue.GetSearchRequestsForEvents(new[] { new LiveEvent(a.userId, Db.UserAction.create, EventType.activity, a.id) });
            var result = await searcher.SearchUnrestricted(request);

            AssertSimpleActivityListenResult(result.data, content, a);
            ////Now, make sure the result contains content, activity, and user results.
            //Assert.True(result.data.ContainsKey("content"));
            //Assert.True(result.data.ContainsKey("activity"));
            //Assert.True(result.data.ContainsKey("user"));

            ////The content, when requested, MUST have permissions!!
            //Assert.True(result.data["content"].First().ContainsKey("permissions"));

            //var content = searcher.ToStronglyTyped<ContentView>(result.data["content"]);
            //var activity = searcher.ToStronglyTyped<ActivityView>(result.data["activity"]);
            //var user = searcher.ToStronglyTyped<UserView>(result.data["user"]);

            //Assert.Single(content);
            //Assert.Single(activity);
            //Assert.Single(user);

            //Assert.Equal(1, user.First().id);
            //Assert.Equal(1, content.First().id); //ALl activity should still point to content 1
            //Assert.Equal(1, activity.First().contentId);
            //Assert.Equal(a.id, activity.First().id);
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
            var request = queue.GetSearchRequestsForEvents(new[] { new LiveEvent(c.createUserId, Db.UserAction.create, EventType.comment, c.id) });
            var result = await searcher.SearchUnrestricted(request);

            //Now, make sure the result contains content, comment, and user results.
            Assert.True(result.data.ContainsKey("content"));
            Assert.True(result.data.ContainsKey("comment"));
            Assert.True(result.data.ContainsKey("user"));

            //The content, when requested, MUST have permissions!!
            Assert.True(result.data["content"].First().ContainsKey("permissions"));

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

    [Fact]
    public async Task AddEventAsync_EventAdded()
    {
        var activities = await GetActivityForContentAsync(1);
        var a = activities.First();
        var evnt = new LiveEvent(a.userId, Db.UserAction.create, EventType.activity, a.id);
        var result = await queue.AddEventAsync(evnt);

        var checkpoint = await queue.ListenEventsAsync(-1, safetySource.Token);

        //Should not fail or throw an exception, the events should be returned...
        Assert.Single(checkpoint.Data);
        Assert.Equal(evnt.userId, checkpoint.Data.First().userId);
        Assert.Equal(evnt.action, checkpoint.Data.First().action);
        Assert.Equal(evnt.type, checkpoint.Data.First().type);
        Assert.Equal(evnt.refId, checkpoint.Data.First().refId);
    }

    //Just a simple test with full integration to see if writing content in the writer will cause a listener to complete with the 
    //event and data we expect.
    [Fact]
    public async Task FullCombo_SimpleActivity()
    {
        var userId = (int)UserVariations.Super;

        //Write content, get content.
        var user = await searcher.GetById<UserView>(RequestType.user, userId);
        var page = GetNewPageView();
        var writtenPage = await writer.WriteAsync(page, userId); //this is NOT a super user

        //Now if we wait for the new data from the beginning, we should get a collection with certain values in it...
        var liveData = await queue.ListenAsync(user, -1, safetySource.Token);

        Assert.True(liveData.optimized);
        Assert.True(liveData.lastId > 0, "LiveData didn't return a positive lastId after an event was clearly retrieved!");
        Assert.Single(liveData.events);
        Assert.Equal(liveData.lastId, liveData.events.Max(x => x.id));

        Assert.Contains(EventType.activity, liveData.data.Keys);

        //Go find the activity we're pointing to...
        var activity = await GetActivityForContentAsync(writtenPage.id);

        //Note: we don't have to get too technical with the event tests, because we already tested to see if the writer is emitting appropriate events. This is just testing
        //to see if we GET the events we expect
        Assert.Equal(UserAction.create, liveData.events.First().action);
        Assert.Equal(activity.First().id, liveData.events.First().refId);
        Assert.Equal(userId, liveData.events.First().userId);

        AssertSimpleActivityListenResult(liveData.data[EventType.activity], writtenPage, activity.First());
    }

    //Another simple full integration test, but ensuring that the expected operations happen when non-optimization happens 
    //(reconnects)
    [Fact]
    public async Task FullCombo_OptimizedAndNot()
    {
        //Force the cache to invalidate every time
        config.DataCacheExpire = System.TimeSpan.Zero;

        var userId = (int)UserVariations.Super;

        //Write content, get content.
        var user = await searcher.GetById<UserView>(RequestType.user, userId);
        var page = GetNewPageView();
        var writtenPage = await writer.WriteAsync(page, userId); //this is NOT a super user

        //Now if we wait for the new data from the beginning, we should get a collection with certain values in it...
        var liveData = await queue.ListenAsync(user, -1, safetySource.Token);

        //This is a read of all cached data, so it should be optimized
        Assert.True(liveData.optimized);

        //But this update will clear the other event. Reading just the last event should give an optimized result, but reading further
        //back will NOT.
        writtenPage.name = "DEFINITELY NOT THE ORIGINAL NAME";
        writtenPage = await writer.WriteAsync(writtenPage, userId); //this is NOT a super user

        //This will read the LAST event, which should be a single optimized read
        var liveData2 = await queue.ListenAsync(user, liveData.lastId, safetySource.Token);
        Assert.True(liveData2.optimized);

        Assert.True(liveData2.lastId > liveData.lastId, "LiveData didn't return the next id??");
        Assert.Single(liveData2.events);
        Assert.Equal(liveData2.lastId, liveData2.events.Max(x => x.id));

        Assert.Contains(EventType.activity, liveData2.data.Keys);

        //Go find the activity we're pointing to...
        var activity = (await GetActivityForContentAsync(writtenPage.id)).OrderBy(x => x.id);

        //Note: we don't have to get too technical with the event tests, because we already tested to see if the writer is emitting appropriate events. This is just testing
        //to see if we GET the events we expect
        Assert.Equal(UserAction.update, liveData2.events.First().action);
        Assert.Equal(activity.Last().id, liveData2.events.First().refId);
        Assert.Equal(userId, liveData2.events.First().userId);

        AssertSimpleActivityListenResult(liveData2.data[EventType.activity], writtenPage, activity.Last());
    }
}