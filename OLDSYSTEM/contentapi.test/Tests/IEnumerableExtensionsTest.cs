using Xunit;
using contentapi.Services.Extensions;
using System.Collections.Generic;
using System;

namespace contentapi.test
{
    public class IEnumerableExtensionsTest : UnitTestBase
    {
        [Fact]
        public void OnlySingleSingle()
        {
            Assert.Equal(5, (new[] {5}).OnlySingle());
        }

        [Fact]
        public void OnlySingleEmpty()
        {
            Assert.Null((new List<string>()).OnlySingle());
        }

        [Fact]
        public void OnlySingleMultiple()
        {
            Assert.ThrowsAny<InvalidOperationException>(() => (new[] { 1, 2, 3}).OnlySingle());
        }
    }
}