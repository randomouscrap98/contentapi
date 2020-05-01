using contentapi.Services.Implementations;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
    public class ActivityServiceTests : ServiceTestBase<ActivityService>
    {
        [Fact]
        public void SimpleMakeActivity()
        {
            //Just make sure it doesn't throw a fit
            var relation = service.MakeActivity(NewEntity(5), 8, keys.CreateAction);
            Assert.NotNull(relation);
            Assert.True(relation.entityId1 != 0); //The relation should map two things together DEFINITELY
            Assert.True(relation.entityId2 != 0);
        }

        [Fact]
        public void SimplePassthrough()
        {
            var relation = service.MakeActivity(NewEntity(99, "blegh"), 8, keys.UpdateAction);
            var view = service.ConvertToView(relation);
            Assert.Equal(99, view.contentId);
            Assert.Equal(8, view.userId);
            Assert.Equal("blegh", view.contentType);
        }
    }
}