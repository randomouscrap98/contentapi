using System;
using Xunit;
using System.IO;

namespace contentapi.test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            Assert.True(File.Exists("content.db"));
            //throw new InvalidOperationException("NOPE");
        }
    }
}
