using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
   [Collection("ASYNC")]
    public class RelationListenerTests : ReadTestBaseExtra
    {
        protected RelationListenerService listener;

        public RelationListenerTests() : base()
        {
            listener = CreateService<RelationListenerService>(true);
        }

        [Fact]
        public void WatchNeverInstant() //This test is a little useless now with the new explicit relations but whatever
        {
            var requester = new Requester() { userId = unit.commonUser.id };
            var watch = watchService.WriteAsync(new WatchView() { contentId = unit.commonContent.id }, requester).Result;
            var listen = listener.ListenAsync(new RelationListenConfig(), requester, cancelToken);

            //This should NOT instant complete even though we have a watch!
            AssertNotWait(listen);

            //OK NOW it should complete
            var comment = commentService.WriteAsync(new CommentView() { content = "WOW", parentId = unit.commonContent.id }, requester).Result;

            var result = AssertWait(listen);

            Assert.Single(result);
            Assert.Equal(comment.id, result.First().id);
        }
        
        [Fact]
        public void WatchComplete()
        {
            var requester = new Requester() { userId = unit.commonUser.id };

            var listen = listener.ListenAsync(new RelationListenConfig(), requester, cancelToken);

            Task.Delay(50).ContinueWith((t)  => 
            {
                var watch = watchService.WriteAsync(new WatchView() { contentId = unit.commonContent.id }, requester).Result;
                var result = AssertWait(listen);
                Assert.Single(result);
                Assert.Equal(watch.id, result.First().id);
            }).Wait();
        }

        [Theory]
        [InlineData("delete")]
        [InlineData("edit")]
        [InlineData("clear")]
        public void WatchEditDeleteComplete(string action)
        {
            var requester = new Requester() { userId = unit.commonUser.id };
            var watch = watchService.WriteAsync(new WatchView() { contentId = unit.commonContent.id }, requester).Result;
            var listen = listener.ListenAsync(new RelationListenConfig(), requester, cancelToken);

            //This should NOT instant complete even though we have a watch!
            AssertNotWait(listen);

            //OK NOW it should complete. Still must do the stupid delay because HEY the watches MUST be through
            //signal ONLY and perhaps the delete goes through BEFORE we get to the secondary listener part.
            Task.Delay(50).ContinueWith((t) =>
            {
                if (action=="delete")
                {
                    watch = watchService.DeleteAsync(watch.id, requester).Result;
                }
                else if (action=="clear")
                {
                    watch = watchService.ClearAsync(watch, requester).Result;
                }
                else if (action=="edit")
                {
                    watch.lastNotificationId = 999; //IDK, something that could never be a number at this point
                    watch = watchService.WriteAsync(watch, requester).Result;
                }

                var result = AssertWait(listen);

                Assert.Single(result);
                Assert.Equal(watch.id, result.First().entityId1); //INTIMATE KNOWLEDGE OF INNER WORKINGS~! oh well
            }).Wait();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void TestHiding(long hideId)
        {
            //Start listening
            var requester = new Requester() { userId = unit.commonUser.id };
            var otherrequester = new Requester() { userId = unit.specialUser.id };
            var baseStatuses = new Dictionary<long, string>() { { 1, "wow" } };

            //This SHOULD listen forever, so we should be registered now!
            var stdlisten = listener.ListenAsync(new RelationListenConfig() { lastId = 9999, statuses = baseStatuses }, requester, cancelSource.Token);
            AssertNotWait(stdlisten);

            //If you listen now, you'll get their status
            var statuses = listener.GetListenersAsync(new Dictionary<long, Dictionary<long, string>>() { { 1, new Dictionary<long, string>() { {0, ""}}}},
                otherrequester, CancellationToken.None);
            var result = AssertWait(statuses);
            Assert.True(result.ContainsKey(1));
            Assert.True(result[1].RealEqual(new Dictionary<long, string>() { {unit.commonUser.id, "wow"}}));

            unit.commonUser.hidelist.Add(hideId);
            userService.WriteAsync(unit.commonUser, requester).Wait();

            statuses = listener.GetListenersAsync(new Dictionary<long, Dictionary<long, string>>() { { 1, new Dictionary<long, string>() { {0, ""}}}},
                otherrequester, CancellationToken.None);
            result = AssertWait(statuses);
            Assert.True(result.ContainsKey(1)); //There should still be the room
            Assert.Empty(result[1]); //Just with nobody in it now

            //Just to be safe, make sure the other guy is still listening
            AssertNotWait(stdlisten);

            //OK cancel 
            cancelSource.Cancel();
            AssertWaitThrows<OperationCanceledException>(stdlisten);
        }
    }
}