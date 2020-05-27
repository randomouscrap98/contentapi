using contentapi.Services.Extensions;
using Xunit;

namespace contentapi.test
{
    public class ReadTestBaseTest : ReadTestBase
    {
        [Fact]
        public void TestCreateUnit()
        {
            var unit = CreateUnitAsync().Result;

            Assert.True(unit.commonUser.id > 0);
            Assert.True(unit.specialUser.id > 0);
            Assert.NotEqual(unit.commonUser.id, unit.specialUser.id);

            Assert.True(unit.commonContent.id > 0);
            Assert.True(unit.specialContent.id > 0);
            Assert.NotEqual(unit.commonContent.id, unit.specialContent.id);
            Assert.False(unit.commonContent.permissions.RealEqual(unit.specialContent.permissions));
        }
    }
}