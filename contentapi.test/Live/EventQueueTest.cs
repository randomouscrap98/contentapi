using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Db;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;
using QueryResultSet = System.Collections.Generic.IEnumerable<System.Collections.Generic.IDictionary<string, object>>;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class EventQueueTest : ViewUnitTestBase //, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbWriter writer;
    protected IGenericSearch searcher;
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected LiveEventQueue queue;
    protected ICacheCheckpointTracker<LiveEvent> tracker;
    protected IPermissionService permission;
    protected LiveEventQueueConfig config;
    protected CacheCheckpointTrackerConfig trackerConfig;
    protected ShortcutsService shortcuts;

    //The tests here are rather complicated; we can probably simplify them in the future, but for now,
    //I just need a system that REALLY tests if this whole thing works, and that is most reliable if 
    //I just use the (known to work) dbwriter to set up the database in a way we expect.
    public EventQueueTest(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.searcher = fixture.GetGenericSearcher();
        this.mapper = fixture.GetService<IMapper>();
        this.permission = fixture.GetService<IPermissionService>();
        this.shortcuts = fixture.GetService<ShortcutsService>();

        this.config = new LiveEventQueueConfig()
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
        this.queue = new LiveEventQueue(fixture.GetService<ILogger<LiveEventQueue>>(), this.config, this.tracker, fixture.dbFactory, this.permission, this.mapper);
        writer = new DbWriter(fixture.GetService<ILogger<DbWriter>>(), this.searcher, 
            fixture.GetConnection(), fixture.GetService<IViewTypeInfoService>(), this.mapper,
            fixture.GetService<History.IHistoryConverter>(), this.permission, this.queue,
            new DbWriterConfig(), new RandomGenerator(), fixture.GetService<IUserService>()); 


        //Reset it for every test
        fixture.ResetDatabase();
    }

    [Fact]
    public void GetSearchRequestForEvents_RunsWithoutException()
    {
        //Can we at LEAST run it without error? There should be a user and content by id 1, and content 1 is created by user 1
        var request = queue.GetSearchRequestsForEvents(new [] { new LiveEvent(1, UserAction.create, EventType.activity_event, 1) });
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
        var activities = searcher.ToStronglyTyped<ActivityView>(baseResult.objects["activity"]);
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

        Assert.NotEmpty(contents);
        Assert.NotEmpty(activities);
        Assert.NotEmpty(users); //don't know how many users there will be, depends on factors

        #pragma warning disable xUnit2012 //I hate xunit
        Assert.True(users.Any(x => x.id == contents.First().createUserId), $"Couldn't find the create user for content {content.id} ({contents.First().createUserId})"); 
        Assert.True(users.Any(x => x.id == activity.userId), $"Couldn't find the activity user for activity {activity.id} ({activity.userId})"); 
        Assert.True(contents.Any(x => x.id == content.id));
        Assert.True(activities.Any(x => x.contentId == content.id));
        Assert.True(activities.Any(x => x.id == activity.id));
        #pragma warning restore xUnit2012 //I hate xunit
    }

    //First, without actually doing anything with the event part, ensure the core of the service works. Does
    //building a request for events create something we expect?
    [Theory] //A regression made me need to test this with other content
    [InlineData(1, 1)] //This one has no parent
    [InlineData(5, 1)] //This one SHOULD have a parent
    [InlineData(1, 2)] //This one has no parent
    [InlineData(5, 2)] //This one SHOULD have a parent
    public async Task GetSearchRequestForEvents_Activity(long contentId, int skip)
    {
        var content = await searcher.GetById<ContentView>(RequestType.content, contentId);
        var activities = await GetActivityForContentAsync(contentId);
        Assert.True(activities.Count > 1); //It should be greater than 1 for content 1, because of inverse activity amounts

        for(var i = 0; i < activities.Count; i += skip)
        {
            var activitySlice = activities.Skip(i).Take(skip);
            var events = activitySlice.Select(x => new LiveEvent(x.userId, UserAction.create, EventType.activity_event, x.id));

            //The event user shouldn't matter but just in case...
            var request = queue.GetSearchRequestsForEvents(events); //new[] { new LiveEvent(a.userId, Db.UserAction.create, EventType.activity_event, a.id) });
            var result = await searcher.SearchUnrestricted(request);

            foreach(var a in activitySlice)
                AssertSimpleActivityListenResult(result.objects, content, a);
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
            type = "message",
            fields = "*",
            query = "contentId = @id"
        });
        var baseResult = await searcher.SearchUnrestricted(search);
        var comments= searcher.ToStronglyTyped<MessageView>(baseResult.objects["message"]);

        Assert.True(comments.Count > 1); //It should be greater than 1 for content 1, because of inverse activity amounts
        foreach(var c in comments)
        {
            //The event user shouldn't matter but just in case...
            var request = queue.GetSearchRequestsForEvents(new[] { new LiveEvent(c.createUserId, UserAction.create, EventType.message_event, c.id) });
            var result = await searcher.SearchUnrestricted(request);

            //Now, make sure the result contains content, comment, and user results.
            Assert.True(result.objects.ContainsKey("content"));
            Assert.True(result.objects.ContainsKey("message"));
            Assert.True(result.objects.ContainsKey("user"));

            //The content, when requested, MUST have permissions!!
            Assert.True(result.objects["content"].First().ContainsKey("permissions"));

            var content = searcher.ToStronglyTyped<ContentView>(result.objects["content"]);
            var comment = searcher.ToStronglyTyped<MessageView>(result.objects["message"]);
            var user = searcher.ToStronglyTyped<UserView>(result.objects["user"]);

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
        var evnt = new LiveEvent(a.userId, UserAction.create, EventType.activity_event, a.id);
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

        Assert.Contains(EventType.activity_event, liveData.objects.Keys);

        //Go find the activity we're pointing to...
        var activity = await GetActivityForContentAsync(writtenPage.id);

        //Note: we don't have to get too technical with the event tests, because we already tested to see if the writer is emitting appropriate events. This is just testing
        //to see if we GET the events we expect
        Assert.Equal(UserAction.create, liveData.events.First().action);
        Assert.Equal(activity.First().id, liveData.events.First().refId);
        Assert.Equal(userId, liveData.events.First().userId);

        AssertSimpleActivityListenResult(liveData.objects[EventType.activity_event], writtenPage, activity.First());
    }

    //Another simple full integration test, but ensuring that the expected operations happen when non-optimization happens 
    //(reconnects). THIS IS AN EXCEPTIONALLY COMPLEX TEST, consider making some baseline work and moving some of these tests around.
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

        Assert.Contains(EventType.activity_event, liveData2.objects.Keys);

        //Go find the activity we're pointing to...
        var activity = (await GetActivityForContentAsync(writtenPage.id)).OrderBy(x => x.id);

        //Note: we don't have to get too technical with the event tests, because we already tested to see if the writer is emitting appropriate events. This is just testing
        //to see if we GET the events we expect
        Assert.Equal(UserAction.update, liveData2.events.First().action);
        Assert.Equal(activity.Last().id, liveData2.events.First().refId);
        Assert.Equal(userId, liveData2.events.First().userId);

        AssertSimpleActivityListenResult(liveData2.objects[EventType.activity_event], writtenPage, activity.Last());

        //OK but now if we try to read both, it should not be optimized 
        liveData = await queue.ListenAsync(user, -1, safetySource.Token);
        Assert.False(liveData.optimized);

        //And now just make sure we have two events and all that
        Assert.Equal(2, liveData.events.Count);
        AssertSimpleActivityListenResult(liveData.objects[EventType.activity_event], writtenPage, activity.First());
        AssertSimpleActivityListenResult(liveData.objects[EventType.activity_event], writtenPage, activity.Last());
    }

    //Ensure private pages don't alert listeners
    [Fact]
    public async Task FullCombo_Privacy()
    {
        //Force the cache to invalidate every time
        config.DataCacheExpire = System.TimeSpan.Zero;

        var userId = (int)UserVariations.Super;
        var listenUserId = (int)UserVariations.Super + 1;

        //Write content, get content.
        var user = await searcher.GetById<UserView>(RequestType.user, userId);
        var listenUser = await searcher.GetById<UserView>(RequestType.user, listenUserId);
        var page = GetNewPageView();
        page.permissions.Clear(); //NO PERMISSIONS, private except for 
        var writtenPage = await writer.WriteAsync(page, userId); //this is NOT a super user

        //Now if we wait for the new data from the beginning, we should get a collection with certain values in it...
        var liveData = await queue.ListenAsync(user, -1, safetySource.Token);
        var activity = (await GetActivityForContentAsync(writtenPage.id)).OrderBy(x => x.id);

        //The user themselves should've been able to get it.
        Assert.True(liveData.optimized);
        AssertSimpleActivityListenResult(liveData.objects[EventType.activity_event], writtenPage, activity.First());

        //And if they ask again, it should still be there
        liveData = await queue.ListenAsync(user, -1, safetySource.Token);
        Assert.True(liveData.optimized);
        AssertSimpleActivityListenResult(liveData.objects[EventType.activity_event], writtenPage, activity.First());

        //But then if other random user comes along, nope
        cancelSource.CancelAfter(10);
        try
        {
            liveData = await queue.ListenAsync(listenUser, -1, cancelSource.Token);
            Assert.False(true, "EVENT PRIVACY: Other user got events from private room!");
        }
        catch(Exception ex)
        {
            Assert.True(ex is OperationCanceledException || ex is TaskCanceledException, "LISTEN TASK DID NOT GET CANCELLED WHEN ASKED!");
        }
    }

    [Fact]
    public async Task FullCombo_MessageAndMessageEngagement()
    {
        //Need a message to put engagement on
        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId);
        var message = GetNewCommentView(AllAccessContentId);
        var writtenMessage = await writer.WriteAsync(message, NormalUserId); //this is NOT a super user

        //Now if we wait for the new data from the beginning, we should get a collection with certain values in it...
        var liveData = await queue.ListenAsync(user, -1, safetySource.Token);

        Assert.True(liveData.optimized);
        Assert.True(liveData.lastId > 0, "LiveData didn't return a positive lastId after an event was clearly retrieved!");
        Assert.Single(liveData.events);
        Assert.Equal(liveData.lastId, liveData.events.Max(x => x.id));

        Assert.Contains(EventType.message_event, liveData.objects.Keys);

        //Note: we don't have to get too technical with the event tests, because we already tested to see if the writer is emitting appropriate events. This is just testing
        //to see if we GET the events we expect
        Assert.Equal(UserAction.create, liveData.events.First().action);
        Assert.Equal(writtenMessage.id, liveData.events.First().refId);
        Assert.Equal(NormalUserId, liveData.events.First().userId);

        //OK whatever, what we really care about is user engagement
        var engagement = new MessageEngagementView()
        {
            messageId = writtenMessage.id,
            type = "vote",
            engagement = "good"
        };
        var writtenEngagement = await writer.WriteAsync(engagement, NormalUserId);

        //Now we listen again but from the next spot
        var liveData2 = await queue.ListenAsync(user, liveData.lastId, safetySource.Token);

        //We should still have just one event, and it'll LOOK like a message event (it is)
        Assert.Contains(EventType.message_event, liveData2.objects.Keys);
        Assert.Single(liveData2.events);
        Assert.Equal(UserAction.update, liveData2.events.First().action);
        Assert.Equal(writtenMessage.id, liveData2.events.First().refId);
        Assert.Equal(NormalUserId, liveData2.events.First().userId);

        var liveMessages = searcher.ToStronglyTyped<MessageView>(liveData2.objects[EventType.message_event][RequestType.message.ToString()]);
        Assert.Contains("vote", liveMessages.First().engagement.Keys);
        Assert.Contains("good", liveMessages.First().engagement["vote"].Keys);
        Assert.Equal(1, liveMessages.First().engagement["vote"]["good"]);

        //AssertSimpleActivityListenResult(liveData2.objects[EventType.message_event], writtenPage, activity.First());
    }

    [Fact]
    public async Task Regression_OldMessagesExposeNewParent()
    {
        //Force the cache to invalidate every time
        config.DataCacheExpire = System.TimeSpan.Zero;

        //Write comments to public room
        const int COMMENTCOUNT = 5;
        var comments = Enumerable.Range(0, COMMENTCOUNT).Select(x => GetNewCommentView(AllAccessContentId));
        var writeComments = new List<MessageView>();

        foreach(var c in comments)
            writeComments.Add(await writer.WriteAsync(c, NormalUserId));
        
        //move comments to private room
        await shortcuts.RethreadMessagesAsync(writeComments.Select(x => x.id).ToList(), SuperAccessContentId, SuperUserId);

        //Now read by normal user. The resultset should not have the private room or messages
        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId);
        var events = await queue.ListenAsync(user, 0, safetySource.Token);

        Assert.True(events.lastId >= COMMENTCOUNT); //We should've done multiple things

        var allIds = writeComments.Select(x => x.id).ToList();
        allIds.Add(SuperAccessContentId);

        Assert.DoesNotContain(events.objects.SelectMany(x => x.Value.SelectMany(y => y.Value)), x => 
        {
            return x.ContainsKey("id") && allIds.Contains((long)x["id"]);
        });
    }

    [Theory]
    [InlineData(NormalUserId, NormalUserId, true)]
    [InlineData(NormalUserId, SuperUserId, false)]
    [InlineData(NormalUserId, 0, false)]
    [InlineData(SuperUserId, SuperUserId, true)]
    [InlineData(SuperUserId, NormalUserId, false)]
    [InlineData(SuperUserId, 0, false)]
    public async Task ListenAsync_WatchPrivacy(long writerId, long listenerId, bool allowed)
    {
        //Write some trash content that anyone can read so our listener completes instantly
        var content = GetNewPageView();
        content.permissions[0] = "CRUD";
        var writtenContent = await writer.WriteAsync(content, NormalUserId);

        //Ensure only the user themselves are getting the watch data
        var watch = await writer.WriteAsync(new WatchView() { contentId = AllAccessContentId }, writerId);

        //See what events we can get. Use a fake user view because nothing in there matters
        var events = await queue.ListenAsync(new UserView() { id = listenerId, super = listenerId == SuperUserId }, -1, safetySource.Token);

        if(allowed)
        {
            Assert.Contains(events.events, x => x.type == nameof(EventType.watch_event) && x.refId == watch.id);

            //Since we're here anyway, might as well ensure the content is pulled
            Assert.Contains(events.objects[EventType.watch_event]["content"], x => (long)x["id"] == watch.contentId);
        }
        else
        {
            Assert.DoesNotContain(events.events, x => x.type == nameof(EventType.watch_event) && x.refId == watch.id);
        }
    }

    [Fact]
    public async Task ListenAsync_UserVariable_Privacy()
    {
        //First, set up two listeners
        var ourUser = await searcher.GetById<UserView>(RequestType.user, NormalUserId, true);
        var otherUser = await searcher.GetById<UserView>(RequestType.user, SuperUserId, true);

        var ourListener = queue.ListenAsync(ourUser, -1, safetySource.Token);
        var otherListener = queue.ListenAsync(otherUser, -1, safetySource.Token);

        //Now, write a variable for our user
        var variable = new UserVariableView()
        {
            key = "somekey",
            value = "trash"
        };

        var writtenVariable = await writer.WriteAsync(variable, ourUser.id);

        var result = await ourListener;
        Assert.Contains(result.events, x => x.refId == writtenVariable.id && x.userId == ourUser.id);
        Assert.Contains(EventType.uservariable_event, result.objects.Keys);
        Assert.Contains("uservariable", result.objects[EventType.uservariable_event].Keys);
        Assert.Contains(result.objects[EventType.uservariable_event]["uservariable"], x => (long)x["id"] == writtenVariable.id && (string)x["key"] == "somekey");

        Assert.False(otherListener.Wait(10));
        safetySource.Cancel();

        try { await otherListener; }
        catch(TaskCanceledException) {}
        catch(OperationCanceledException) {}
    }

    [Fact]
    public async Task ListenAsync_WatchRelatedContent_Delete()
    {
        var watch = await writer.WriteAsync(new WatchView() { contentId = AllAccessContentId }, NormalUserId);

        var originalEvents = await queue.ListenAsync(new UserView() { id = NormalUserId }, -1, safetySource.Token);

        //Then immediately delete it
        var deletedWatch = await writer.DeleteAsync<WatchView>(watch.id, NormalUserId);

        //See what events we can get. Use a fake user view because nothing in there matters
        var events = await queue.ListenAsync(new UserView() { id = NormalUserId }, originalEvents.lastId, safetySource.Token);

        Assert.Single(events.events);

        //Make sure there's a watch delete event and that it has "related_content"
        var deleteEvent = events.events.First(x => x.action == UserAction.delete && x.type == nameof(EventType.watch_event));

        Assert.Equal(AllAccessContentId, deleteEvent.contentId);
        Assert.Contains(nameof(RequestType.content), events.objects[EventType.watch_event].Keys);
        Assert.Contains(events.objects[EventType.watch_event][nameof(RequestType.content)], x => (long)x["id"] == watch.contentId);

        //Assert.All(originalEvents.objects, x => Assert.NotEqual("related_content", x));
        //Also, make sure that related content doesn't leak into OTHER results
        //Assert.Contains(Constants.RelatedContentKey, events.objects[EventType.activity_event].Keys);
        //Assert.Empty(events.objects[EventType.activity_event][Constants.RelatedContentKey]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-23)]
    [InlineData(null)]
    public async Task Regression_ListenAsync_NoInstantComplete(int? id)
    {
        var user = new UserView() { id = NormalUserId };
        Task<LiveData> waiter = id == null ? queue.ListenAsync(user, token : safetySource.Token) : queue.ListenAsync(user, id.Value, safetySource.Token);
        Assert.False(waiter.Wait(10));

        safetySource.Cancel();

        try { await waiter; }
        catch(TaskCanceledException) {}
        catch(OperationCanceledException) {}
    }

    [Fact]
    public async Task Regression_ListenAsync_UidsInTextMessage()
    {
        var message = GetNewCommentView(AllAccessContentId);
        message.text = $"%{(int)UserVariations.Special + 1}% is not %{(int)UserVariations.Special + 2}%'s friend";
        var writtenMessage = await writer.WriteAsync(message, NormalUserId);

        //And now, the event should be there AND the data should include those two users
        var eventData = await queue.ListenAsync(new UserView() { id = NormalUserId }, 0, safetySource.Token);

        Assert.Contains(eventData.events, x => x.refId == writtenMessage.id && x.action == UserAction.create);
        Assert.Contains(EventType.message_event, eventData.objects.Keys);
        Assert.Contains("user", eventData.objects[EventType.message_event].Keys);

        Assert.Contains(eventData.objects[EventType.message_event]["user"], x => (long)x["id"] == (int)UserVariations.Special + 1);
        Assert.Contains(eventData.objects[EventType.message_event]["user"], x => (long)x["id"] == (int)UserVariations.Special + 2);
    }

    [Fact]
    public async Task Regression_ListenAsync_ReceiveUserPermissions()
    {
        //Set up a module message with a recipient
        var message = GetNewCommentView(AllAccessContentId);
        message.module = "test";
        message.receiveUserId = NormalUserId;

        //Set up our two listeners
        var ourUser = await searcher.GetById<UserView>(RequestType.user, NormalUserId, true);
        var otherUser = await searcher.GetById<UserView>(RequestType.user, SuperUserId, true);

        var ourListener = queue.ListenAsync(ourUser, -1, safetySource.Token);
        var otherListener = queue.ListenAsync(otherUser, -1, safetySource.Token);

        //Now, write the message
        var writtenMessage = await writer.WriteAsync(message, ourUser.id);

        var result = await ourListener;
        Assert.Contains(result.events, x => x.refId == writtenMessage.id && x.userId == ourUser.id);
        Assert.Contains(EventType.message_event, result.objects.Keys);
        Assert.Contains("message", result.objects[EventType.message_event].Keys);
        Assert.Contains(result.objects[EventType.message_event]["message"], x => (long)x["id"] == writtenMessage.id && (string)x["module"] == "test" && (long)x["receiveUserId"] == ourUser.id);

        Assert.False(await Task.Run(() => otherListener.Wait(10)));
        safetySource.Cancel();

        try { await otherListener; }
        catch(TaskCanceledException) {}
        catch(OperationCanceledException) {}
    }

    [Fact]
    public async Task Regression_ListenAsync_OverflowMessages()
    {
        var id = queue.GetCurrentLastId();

        //Write 10 messages
        for(var i = 0; i < 10; i++)
        {
            var message = GetNewCommentView(AllAccessContentId);
            message.text = $"Message {i}";
            var writtenMessage = await writer.WriteAsync(message, NormalUserId);
        }

        var user = await searcher.GetById<UserView>(RequestType.user, NormalUserId, true);

        //Now, given some funny small configured number of max events to pull, see if we can repeatedly get those amount of events
        config.MaxEventListen = 2;

        for(var i = 0; i < 10; i += config.MaxEventListen)
        {
            Assert.NotEqual(id, queue.GetCurrentLastId());
            var result = await queue.ListenAsync(user, id, safetySource.Token);
            Assert.Equal(2, result.events.Count);
            Assert.Equal(2, result.objects[EventType.message_event]["message"].Count());
            id = result.lastId;
        }

        Assert.Equal(id, queue.GetCurrentLastId());
    }
}