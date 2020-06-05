using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace contentapi.test
{
    [Collection("ASYNC")]
    public class ReadTestListen : ReadTestBaseExtra
    {
        protected ChainService chainer;

        public ReadTestListen() : base()
        {
            chainer = CreateService<ChainService>(true);
        }

        public Task<ListenResult> BasicListen(ListenerChainConfig lConfig, RelationListenChainConfig rConfig, long requesterId)
        {
            return chainer.ListenAsync(null, lConfig, rConfig, new Requester() { userId = requesterId }, cancelToken);
        }

        public List<string> BasicCommentChain()
        {
            return new List<string>() { "comment.0id" };
        }

        public Tuple<Func<CommentView>, Action<Task<ListenResult>>> GenSimpleCommentThroughput()
        {
            //This is "captured" by the functions + actions
            CommentView comment = null;

            Func<CommentView> create = () => comment = commentService.WriteAsync(new CommentView() { content = "hello", parentId = unit.commonContent.id }, new Requester() { userId = unit.specialUser.id}).Result;
            Action<Task<ListenResult>> check = (listen) =>
            {
                //Ensure the other completed
                var complete = AssertWait(listen);
                Assert.Contains("comment", complete.chain.Keys);
                Assert.Single(complete.chain["comment"]);
                Assert.Equal(comment.id, ((dynamic)complete.chain["comment"].First()).id);
                Assert.Equal(comment.content, ((dynamic)complete.chain["comment"].First()).content);
            };

            return Tuple.Create(create, check);
        }

        //Can a person listening see a comment from someone else?
        [Fact]
        public void SimpleListen()
        {
            //First, start listening for any comment
            var listen = BasicListen(null, new RelationListenChainConfig() { chain = BasicCommentChain() }, unit.commonUser.id);

            //There is an acceptable nuance to listening: since we are just saving the task and continuing, there is a window 
            //of time where neither the initial query (for instant complete) nor the actual listening will find a comment/etc written
            //during that window. Thus, wait enough time for the initial query to complete (let's HOPE it's enough time!!!)
            Task.Delay(50).ContinueWith((t) =>
            {
                var actions = GenSimpleCommentThroughput();

                //Now simply call both actions
                actions.Item1();
                actions.Item2(listen);
            }).Wait();
        }

        [Fact]
        public void SimpleInstantComplete()
        {
            var actions = GenSimpleCommentThroughput();

            //Generate the comment FIRST so there's something to pickup immediately
            var comment = actions.Item1();

            //Listen for comment ids BEFORE the last comment so we complete instantly
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id - 1, chain = BasicCommentChain() }, unit.commonUser.id);

            //Now just call item 2! Done!
            actions.Item2(listen);
        }

        [Fact]
        public void SimpleInstantSecret()
        {
            //Write a comment BUT in the seeeecret area!
            var comment = commentService.WriteAsync(new CommentView() { content = "hello", parentId = unit.specialContent.id }, new Requester() { userId = unit.specialUser.id}).Result;

            //Listen for comment ids BEFORE the last comment so we would "normally" complete instantly (but we shouldn't be able to read this comment)
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id - 1, chain = BasicCommentChain() }, unit.commonUser.id);

            //You should not receive anything! It was a comment in a room you don't have access to, regardless of the "magic" mega listener!
            AssertNotWait(listen);

            //You should PROBABLY cancel everything
            cancelSource.Cancel();
            AssertWaitThrows<OperationCanceledException>(listen);
        }

        [Fact]
        public void SimpleInstantEdit()
        {
            var actions = GenSimpleCommentThroughput();

            //Generate the comment FIRST so there's something to pickup immediately
            var comment = actions.Item1();

            //Now edit the comment to generate a new "event"
            comment.content = "oh it was edited!";
            var newComment = commentService.WriteAsync(comment, new Requester() {userId = unit.specialUser.id }).Result;

            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id , chain = BasicCommentChain() }, unit.commonUser.id);

            //Now just call item 2! Done!
            actions.Item2(listen);
        }

        [Fact]
        public void SimpleInstantDelete()
        {
            var actions = GenSimpleCommentThroughput();

            //Generate the comment FIRST so there's something to pickup immediately
            var comment = actions.Item1();

            //Now edit the comment to generate a new "event"
            var deletedComment = commentService.DeleteAsync(comment.id, new Requester() { userId = unit.specialUser.id }).Result;

            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id , chain = BasicCommentChain() }, unit.commonUser.id);

            //OK, item 2 is useless this time. Make sure it completes
            var complete = AssertWait(listen);
            Assert.Contains(Keys.ChainCommentDelete, complete.chain.Keys);
            Assert.Single(complete.chain[Keys.ChainCommentDelete]);
            Assert.Equal(comment.id, ((dynamic)complete.chain[Keys.ChainCommentDelete].First()).id);
        }

        public ListenerChainConfig BasicListenConfig(bool specialContent = false)
        {
            return new ListenerChainConfig() { lastListeners = new Dictionary<long, Dictionary<long, string>>() 
                {{ specialContent ? unit.specialContent.id : unit.commonContent.id, new Dictionary<long, string>() {{0, ""}} }} };
        }

        [Fact]
        public void SimpleInstantEmptyListener()
        {
            //FORCE the NON-INSTANT to be SO LONG that it will certainly fail if it skips it
            relationConfig.ListenerPollingInterval = TimeSpan.FromMinutes(10);

            var listen = BasicListen(BasicListenConfig(), null, unit.commonUser.id);

            //it should instantly complete since there aren't actually listeners
            var complete = AssertWait(listen);

            //ALL the parents we give should be returned and ONLY those
            Assert.Single(complete.listeners);
            Assert.True(complete.listeners.ContainsKey(unit.commonContent.id));
            Assert.Empty(complete.listeners[unit.commonContent.id]);
        }

        [Fact]
        public void SimpleWaitEmptyListener()
        {
            var listenConfig = BasicListenConfig();
            listenConfig.lastListeners[unit.commonContent.id].Clear();
            var listen = BasicListen(listenConfig, null, unit.commonUser.id);

            //It should NOT complete since there really are no listeners
            AssertNotWait(listen);

            //You should PROBABLY cancel everything
            cancelSource.Cancel();
            AssertWaitThrows<OperationCanceledException>(listen);
        }

        [Fact]
        public void SimpleListenerTrueEmptyThrows()
        {
            var listenConfig = BasicListenConfig();
            listenConfig.lastListeners.Clear();
            var listen = BasicListen(listenConfig, null, unit.commonUser.id);

            AssertWaitThrows<BadRequestException>(listen);
        }

        [Fact]
        public void SimpleListenerUnreadableThrows()
        {
            var listenConfig = BasicListenConfig(true);
            var listen = BasicListen(listenConfig, null, unit.commonUser.id);

            AssertWaitThrows<BadRequestException>(listen);
        }

        [Fact]
        public void SimpleListenerGarbageThrows()
        {
            var listenConfig = BasicListenConfig();
            listenConfig.lastListeners.Add(99, listenConfig.lastListeners[unit.commonContent.id]);
            listenConfig.lastListeners.Remove(unit.commonContent.id);
            var listen = BasicListen(listenConfig, null, unit.commonUser.id);

            AssertWaitThrows<BadRequestException>(listen);
        }

        [Fact]
        public void InstantWatchChaining()
        {
            //Make user watch ugh
            var requester = new Requester() { userId = unit.commonUser.id };
            var watch = watchService.WriteAsync(new WatchView() { contentId = unit.commonContent.id }, requester).Result;

            //The endpoint SHOULD return watches!
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = 0, chain = new List<string>() { "watch.0id" } }, unit.commonUser.id);

            var complete = AssertWait(listen);
            Assert.Contains("watch", complete.chain.Keys);
            Assert.Single(complete.chain["watch"]);
            Assert.Equal(watch.id, ((dynamic)complete.chain["watch"].First()).id);
            Assert.Equal(watch.contentId, ((dynamic)complete.chain["watch"].First()).contentId);
        }

        [Fact]
        public void InstantWatchEdit()
        {
            //Make user watch ugh
            var requester = new Requester() { userId = unit.commonUser.id };
            var watch = watchService.WriteAsync(new WatchView() { contentId = unit.commonContent.id }, requester).Result;

            //This should NOT complete AND the later stuff should NOT give the watch that was cleared! ONLY THE CHAIN SIGNAL!
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = watch.id, chain = new List<string>() { "watch.0id" } }, unit.commonUser.id);

            AssertNotWait(listen);

            Task.Delay(50).ContinueWith((t) =>
            {
                //now clear notifications I suppose (after creating a comment)
                var comment = commentService.WriteAsync(new CommentView() { content = "hello", parentId = unit.commonContent.id }, requester).Result;
                watch = watchService.ClearAsync(watch, requester).Result;

                var complete = AssertWait(listen);
                Assert.False(complete.chain.ContainsKey("watch") && complete.chain["watch"].Count > 0, "There are watches!");
                Assert.Contains(Keys.ChainWatchUpdate, complete.chain.Keys);
                Assert.Single(complete.chain[Keys.ChainWatchUpdate]);
                Assert.Equal(watch.id, ((dynamic)complete.chain[Keys.ChainWatchUpdate].First()).id);
            }).Wait();
        }
    }
}