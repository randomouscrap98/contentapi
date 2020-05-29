using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Configs;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace contentapi.test
{
    public class ReadTestListen : ReadTestBase
    {
        protected ReadTestUnit unit;
        protected CancellationTokenSource cancelSource;
        protected CancellationToken cancelToken;
        protected ChainService chainer;

        protected SystemConfig config = new SystemConfig()
        { 
            ListenTimeout = TimeSpan.FromSeconds(60),
            ListenGracePeriod = TimeSpan.FromSeconds(10)
        };

        public ReadTestListen() : base()
        {
            unit = CreateUnitAsync().Result;
            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;
            chainer = CreateService<ChainService>(true);
        }

        public override IServiceCollection CreateServices()
        {
            var services = base.CreateServices();
            services.AddSingleton(config);
            return services;
        }

        public Task<ListenResult> BasicListen(ListenerChainConfig lConfig, RelationListenChainConfig rConfig, long requesterId)
        {
            return chainer.ListenAsync(null, lConfig, rConfig, new Requester() { userId = requesterId }, cancelToken);
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
            var listen = BasicListen(null, new RelationListenChainConfig() { chain = new List<string>() { "comment" }}, unit.commonUser.id);

            //There is an acceptable nuance to listening: since we are just saving the task and continuing, there is a window 
            //of time where neither the initial query (for instant complete) nor the actual listening will find a comment/etc written
            //during that window. Thus, wait enough time for the initial query to complete (let's HOPE it's enough time!!!)
            Task.Delay(100).ContinueWith((t) =>
            {
                var actions = GenSimpleCommentThroughput();

                //Now simply call both actions
                actions.Item1();
                actions.Item2(listen);
            });
        }

        [Fact]
        public void SimpleInstantComplete()
        {
            var actions = GenSimpleCommentThroughput();

            //Generate the comment FIRST so there's something to pickup immediately
            var comment = actions.Item1();

            //Listen for comment ids BEFORE the last comment so we complete instantly
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id - 1, chain = new List<string>() { "comment" }}, unit.commonUser.id);

            //Now just call item 2! Done!
            actions.Item2(listen);
        }

        [Fact]
        public void SimpleInstantSecret()
        {
            //Write a comment BUT in the seeeecret area!
            var comment = commentService.WriteAsync(new CommentView() { content = "hello", parentId = unit.specialContent.id }, new Requester() { userId = unit.specialUser.id}).Result;

            //Listen for comment ids BEFORE the last comment so we would "normally" complete instantly (but we shouldn't be able to read this comment)
            var listen = BasicListen(null, new RelationListenChainConfig() { lastId = comment.id - 1, chain = new List<string>() { "comment" }}, unit.commonUser.id);

            //You should not receive anything! It was a comment in a room you don't have access to, regardless of the "magic" mega listener!
            AssertNotWait(listen);

            //You should PROBABLY cancel everything
            cancelSource.Cancel();
            AssertWaitThrows<OperationCanceledException>(listen);
        }
    }
}