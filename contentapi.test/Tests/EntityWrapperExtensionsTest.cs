using Randomous.EntitySystem;
using Xunit;
using contentapi.Services.Extensions;
using contentapi.Models;
using System;

namespace contentapi.test
{
    public class EntityWrapperExtensionsTest : UnitTestBase //ServiceTestBase<IEntityProvider>
    {
        [Fact]
        public void QuickEntity()
        {
            var entity = EntityWrapperExtensions.QuickEntity("someName", "someContent");
            Assert.Equal("someName", entity.name);
            Assert.Equal("someContent", entity.content);
            Assert.True((DateTime.Now - entity.createDate).TotalSeconds < 5);
        }

        [Fact]
        public void AddValue()
        {
            var entity = EntityWrapperExtensions.QuickEntity("aname")
                .AddValue("key1", "value1")
                .AddValue("key2", "value2");
            
            Assert.Equal(2, entity.Values.Count);
            Assert.Equal("key1", entity.Values[0].key);
            Assert.Equal("value2", entity.Values[1].value);
        }
    }
}