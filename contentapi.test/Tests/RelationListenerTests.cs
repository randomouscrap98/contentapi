using System;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Configs;
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
    }
}