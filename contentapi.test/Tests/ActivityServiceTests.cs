using contentapi.Services.Implementations;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
    public class ActivityServiceTests : ServiceTestBase<ActivityService>
    {
        Keys keys;

        public ActivityServiceTests()
        {
            this.keys = CreateService<Keys>();
        }

        protected Entity NewEntity()
        {
            return new Entity()
            {
                id = 5
            };
        }

        [Fact]
        public void SimpleMakeActivity()
        {
            //Just make sure it doesn't throw a fit
            var relation = service.MakeActivity(NewEntity(), 8, keys.CreateAction);
            Assert.NotNull(relation);
            Assert.True(relation.entityId1 != 0); //The relation should map two things together DEFINITELY
            Assert.True(relation.entityId2 != 0);
        }
    }
}