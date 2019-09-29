using contentapi.Controllers;
using Xunit;
using contentapi.Models;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersController>
    {
        [Fact]
        public void TestBasicUserCreate()
        {
            var result = controller.Post(new UserCredential()
            {
                username = "abcTestUserxyz",
                email = "nothing@definitelynothing_noforReAL.com",
                password = "aDefinitePassword123"
            }).Result;

            Assert.True(result.Value.username == "abcTestUserxyz");
        }
    }
}