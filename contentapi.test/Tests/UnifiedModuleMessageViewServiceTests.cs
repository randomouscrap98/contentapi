using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Xunit;

namespace contentapi.test
{
    public class UnifiedModuleMessageViewServiceTests : ReadTestBase //ServiceTestBase<UnifiedModuleMessageViewService>
    {
        protected Mapper mapper;
        protected UnifiedModuleMessageViewService service;
        //protected CommentViewService commentService;
        protected ReadTestUnit unit;

        protected Requester commonRequester;
        protected Requester specialRequester;

        public UnifiedModuleMessageViewServiceTests()
        {
            mapper = CreateService<Mapper>();
            service = CreateService<UnifiedModuleMessageViewService>();
            //commentService = 
            unit = CreateUnitAsync().Result;
            commonRequester = new Requester() { userId = unit.commonUser.id };
            specialRequester = new Requester() { userId = unit.specialUser.id };
        }

        public UnifiedModuleMessageView GetUserMessage(long receiver, long sendUserId, long id = 0)
        {
            return new UnifiedModuleMessageView()
            {
                id = id,
                createDate = DateTime.Now,
                module = "test",
                message = "wowzers, this sure TEST is | weird?",
                sendUserId = sendUserId,
                receiveUserId = receiver
            };
        }
    
        public UnifiedModuleMessageView GetRoomMessage(long room, long sendUserId, long id = 0)
        {
            return new UnifiedModuleMessageView()
            {
                id = id,
                createDate = DateTime.Now,
                module = "test",
                message = "wowzers, this sure TEST is | weird?",
                sendUserId = sendUserId,
                parentId = room
            };
        }


        [Fact]
        public void TestConvertSearch()
        {
            var search = new UnifiedModuleMessageViewSearch()
            {
                CreateStart = DateTime.Now.AddDays(-1),
                CreateEnd = DateTime.Now,
                ModuleLike = "test",
                MaxId = 1000,
                MinId = 1,
                ParentIds = new List<long>() { 1, 89, 5 }
            };

            var csearch = mapper.Map<CommentSearch>(search);
            Assert.Equal(search.CreateStart, csearch.CreateStart);
            Assert.Equal(search.MaxId, csearch.MaxId);

            var search2 = mapper.Map<UnifiedModuleMessageViewSearch>(csearch);

            //Let's HOPE these two serialize the same way...
            var sserialize = JsonConvert.SerializeObject(search);
            var s2serialize = JsonConvert.SerializeObject(search2);

            Assert.Equal(sserialize, s2serialize);
        }

        [Fact]
        public void TestConvertRegularView()
        {
            var uview = GetUserMessage(87, 99, 543);
            var mview = mapper.Map<ModuleMessageView>(uview);
            Assert.Equal(uview.id, mview.id);
            Assert.Equal(uview.createDate, mview.createDate);

            var uview2 = mapper.Map<UnifiedModuleMessageView>(mview);

            var sserialize = JsonConvert.SerializeObject(uview);
            var s2serialize = JsonConvert.SerializeObject(uview2);

            Assert.Equal(sserialize, s2serialize);
        }

        [Fact]
        public void TestConvertRoomView()
        {
            var uview = GetRoomMessage(86, 99, 555);
            var mview = mapper.Map<CommentView>(uview);
            Assert.Equal(uview.id, mview.id);
            Assert.Equal(uview.createDate, mview.createDate);

            var uview2 = mapper.Map<UnifiedModuleMessageView>(mview);

            var sserialize = JsonConvert.SerializeObject(uview);
            var s2serialize = JsonConvert.SerializeObject(uview2);

            Assert.Equal(sserialize, s2serialize);
        }

        [Fact]
        public void BasicAddUserMessage()
        {
            //Send a self message
            var baseMessage = GetUserMessage(unit.commonUser.id, unit.commonUser.id);
            var result = service.AddMessageAsync(baseMessage, commonRequester).Result;
            var messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { ReceiverIds = new List<long>() { unit.commonUser.id }}, commonRequester).Result;
            Assert.Single(messages);
            Assert.Equal(baseMessage.message, messages.First().message);
            Assert.Equal(baseMessage.module, messages.First().module);
            Assert.Equal(result.id, messages.First().id);
        }

        [Fact]
        public void BasicAddRoomMessage()
        {
            //Send a room message in the common room
            var baseMessage = GetRoomMessage(unit.commonContent.id, unit.commonUser.id);
            var result = service.AddMessageAsync(baseMessage, commonRequester).Result;

            //And now, there should be NOTHING in comments, this is VERY IMPORTANT since we're piggybacking off the comment system!
            var comments = commentService.SearchAsync(new CommentSearch(), commonRequester).Result;
            Assert.Empty(comments);


            //This kind of search would be searching for users, and you should still get your room messages!
            var messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { ReceiverIds = new List<long>() { unit.commonUser.id }}, commonRequester).Result;

            var checkResult = new Action(() =>
            {
                Assert.Single(messages);
                Assert.Equal(baseMessage.message, messages.First().message);
                Assert.Equal(baseMessage.module, messages.First().module);
                Assert.Equal(result.id, messages.First().id);
            });
            
            checkResult();

            //And this is by room, meaning you should still get it.
            messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { ParentIds = new List<long>() { unit.commonContent.id }}, commonRequester).Result;
            checkResult();

            //And this is just all of them
            messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { ParentIds = new List<long>() { unit.commonContent.id }}, commonRequester).Result;
            checkResult();
        }

        [Fact]
        public void BasicPermissions()
        {
            //Common SHOULD be able read common content
            var entity = service.CanUserDoOnParent(unit.commonContent.id, Services.Constants.Keys.ReadAction, commonRequester).Result;

            //Can't read special
            Task check = service.CanUserDoOnParent(unit.specialContent.id, Services.Constants.Keys.ReadAction, commonRequester);
            AssertWaitThrows<ForbiddenException>(check);

            entity = service.CanUserDoOnParent(unit.specialContent.id, Services.Constants.Keys.ReadAction, specialRequester).Result;
        }

        [Theory]
        //0 = common USER, 1 = special USER, 2 = commonroom, 3 = specialroom
        [InlineData(false, 0, false, true)]     //user sends to self, should be able to read
        [InlineData(false, 1, false, false)]    //user sends to special, should NOT be able to read
        [InlineData(false, 1, true, true)]      //user sends to special, THEY should be able to read
        [InlineData(true, 1, true, true)]       //special sends to self, should be able to read
        [InlineData(true, 0, true, false)]      //even if they're special, messages sent to common should not be readable
        [InlineData(true, 0, false, true)]      //special user messages sent to common readable by common
        [InlineData(false, 2, false, true, true)]       //Now for room stuff, common allowed to write to common
        [InlineData(false, 3, false, false, false)]     //But common write should fail to special room
        [InlineData(false, 2, true, true, true)]        //The special user should be able to get the broadcast message too
        [InlineData(true, 2, true, true, true)]         //Special user writing to special room, can read and write
        [InlineData(true, 3, true, true, true)]         //Special user writing to special room, can read and write
        [InlineData(true, 3, false, false, true)]       //Special user writing to special room, common can't read (IMPORTANT!!!)
        [InlineData(true, 2, false, true, true)]        //Special user writing to common room, common user can read
        public void TestReadable(bool writeUserSpecial, int receiveUserSpecialBroadcast, bool readUserSpecial, bool allowed, bool writeAllowed = true)
        {
            var receiveUser = receiveUserSpecialBroadcast == 0 ? unit.commonUser : receiveUserSpecialBroadcast == 1 ? unit.specialUser : null;
            var receiveRoom = receiveUserSpecialBroadcast == 2 ? unit.commonContent : receiveUserSpecialBroadcast == 3 ? unit.specialContent : null;
            var writeUser = writeUserSpecial ? unit.specialUser : unit.commonUser;
            var readUser = readUserSpecial ? unit.specialUser : unit.commonUser;
            //var = service.AddMessageAsync(baseMessage, new Requester() { userId = writeUser.id } ).Result;
            UnifiedModuleMessageView baseMessage = null;

            if(receiveUser != null)
                baseMessage = GetUserMessage(receiveUser.id, writeUser.id);// unit.commonContent.id, unit.commonUser.id);
            else
                baseMessage = GetRoomMessage(receiveRoom.id, writeUser.id);

            //None of the add message routines should fail
            UnifiedModuleMessageView result = null;

            try
            {
                result = service.AddMessageAsync(baseMessage, new Requester() { userId = writeUser.id } ).Result;
                Assert.True(writeAllowed);
            }
            catch(Exception ex)
            {
                Assert.False(writeAllowed);
                Assert.True(ex is ForbiddenException || ex.InnerException is ForbiddenException);
                return;
            }
        
            //This shouldn't fail either, just the amount of stuff we get!
            var messages = service.SearchAsync(new UnifiedModuleMessageViewSearch(), new Requester() { userId = readUser.id }).Result;

            if(allowed)
            {
                Assert.Single(messages);
                Assert.Equal(baseMessage.message, messages.First().message);
                Assert.Equal(baseMessage.module, messages.First().module);
                Assert.Equal(result.id, messages.First().id);
            }
            else
            {
                Assert.Empty(messages);
            }
        }

        [Fact]
        public void NoCommentsInModuleResults()
        {
            //First, add a comment to common room
            var comment = commentService.WriteAsync(new CommentView() { createUserId = unit.commonUser.id, parentId = unit.commonContent.id, content = "this is the comment"},
                commonRequester).Result;

            Assert.True(comment.id > 0);

            //Next, search for that particular comment in the module system
            var messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { Ids = new List<long>() { comment.id }}, commonRequester).Result;
            Assert.Empty(messages);

            //Now, search for the comment. This should cache it?
            var comments = commentService.SearchAsync(new CommentSearch(){Ids = new List<long>() {comment.id}}, commonRequester).Result;
            Assert.Single(comments);
            Assert.Equal(comment, comments.First());

            //Now, search the modules again. There should be NO module messages!!
            messages = service.SearchAsync(new UnifiedModuleMessageViewSearch() { Ids = new List<long>() { comment.id }}, commonRequester).Result;
            Assert.Empty(messages);
        }

    }
}