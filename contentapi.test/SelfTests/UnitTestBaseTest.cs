using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class UnitTestBaseTest : UnitTestBase
{
    [Fact]
    public void TestLogging()
    {
        logger.LogInformation("Can log in unit tests!");
    }
}