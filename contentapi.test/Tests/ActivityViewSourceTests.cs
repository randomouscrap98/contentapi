using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
    public class ActivityViewSourceTests : ServiceTestBase<ActivityViewSource>
    {
        [Fact]
        public void SimpleMakeActivity()
        {
            //Just make sure it doesn't throw a fit
            var relation = service.MakeActivity(NewEntity(5), 8, Keys.CreateAction);
            Assert.NotNull(relation);
            Assert.True(relation.entityId1 != 0); //The relation should map two things together DEFINITELY
            Assert.True(relation.entityId2 != 0);
        }

        [Fact]
        public void Regression_DoubleType()
        {
            var relation = service.MakeActivity(NewEntity(5, Keys.ContentType + "@wow.yeah"), 8, Keys.CreateAction, "myextra");
            var activity = service.ToView(relation);

            Assert.Equal(Keys.ContentType, activity.type);
            Assert.Equal("@wow.yeah", activity.contentType);
        }

        [Fact]
        public void Regression_DoubleTypeEmpty()
        {
            var relation = service.MakeActivity(NewEntity(5, Keys.FileType), 8, Keys.CreateAction, "myextra");
            var activity = service.ToView(relation);

            Assert.Equal(Keys.FileType, activity.type);
            Assert.True(string.IsNullOrWhiteSpace(activity.contentType));
        }
    }
}