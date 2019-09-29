using System.IO;
using Xunit;

namespace contentapi.test
{
    public class TestTest
    {
        [Fact]
        public void DatabaseExists()
        {
            Assert.True(File.Exists("content.db"));
        }
    }
}