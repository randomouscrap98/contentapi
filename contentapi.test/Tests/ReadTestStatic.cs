using System;
using System.Linq;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    public class ReadTestStatic : ReadTestBase
    {
        protected ReadTestUnit unit;

        public ReadTestStatic() : base()
        {
            unit = CreateUnitAsync().Result;
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, true)]
        [InlineData(false, false, true)]
        public void SimpleWrite(bool commonUser, bool commonContent, bool allowed)
        {
            var comment = new CommentView() {content = "HELLO", parentId = commonContent ? unit.commonContent.id : unit.specialContent.id };
            var requester = new Requester() { userId = commonUser ? unit.commonUser.id : unit.specialUser.id };

            AssertAllowed<ForbiddenException>(() =>
            {
                var view = commentService.WriteAsync(comment, requester).Result;
                Assert.True(view.id > 0);
            }, allowed);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void SimpleRead(bool writeCommon, bool readCommon, bool allowed)
        {
            //Just the simple write
            var view = commentService.WriteAsync(
                new CommentView() { content = "HELLO", parentId = writeCommon ? unit.commonContent.id : unit.specialContent.id }, 
                new Requester() { userId = writeCommon ? unit.commonUser.id : unit.specialUser.id }).Result;
            Assert.True(view.id > 0);

            //Now make sure it shows up only when we want it. It should be our ONLY comment
            var comments = commentService.SearchAsync(new CommentSearch(), new Requester() { userId = readCommon ? unit.commonUser.id : unit.specialUser.id }).Result;

            if(allowed)
                Assert.Contains(view.id, comments.Select(x => x.id));
            else
                Assert.Empty(comments);
        }
    }
}