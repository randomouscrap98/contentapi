using Xunit;

namespace contentapi.test;

public class StaticUtilsTest : UnitTestBase
{
    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(5, "5 seconds")]
    [InlineData(60, "1 minute")]
    [InlineData(120, "2 minutes")]
    [InlineData(60 * 59, "59 minutes")]
    [InlineData(60 * 60, "1 hour")]
    [InlineData(60 * 60 * 5, "5 hours")]
    [InlineData(60 * 60 * 24, "1 day")]
    [InlineData(60 * 60 * 24 * 100, "100 days")]
    [InlineData(60 * 60 * 24 * 365, "1 year")]
    [InlineData(60 * 60 * 24 * 365 * 20, "20 years")]
    [InlineData(0.5, "500 milliseconds")]
    [InlineData(0.001, "1 millisecond")]
    public void HumanTime_All(double seconds, string expected)
    {
        Assert.Equal(expected, StaticUtils.HumanTime(System.TimeSpan.FromSeconds(seconds), 0));
    }

    [Theory]
    [InlineData(1234, 1, "1.2 seconds")]
    [InlineData(1234, 2, "1.23 seconds")]
    [InlineData(1234, 3, "1.234 seconds")]
    [InlineData(1234, 4, "1.2340 seconds")]
    public void HumanTime_Decimal(double milliseconds, int decimals, string expected)
    {
        Assert.Equal(expected, StaticUtils.HumanTime(System.TimeSpan.FromMilliseconds(milliseconds), decimals));
    }
}