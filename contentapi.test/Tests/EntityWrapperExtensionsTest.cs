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
            
            Assert.Empty(entity.Relations);
            Assert.Equal(2, entity.Values.Count);
            Assert.Equal("key1", entity.Values[0].key);
            Assert.Equal("value2", entity.Values[1].value);
        }

        [Fact]
        public void AddRelation()
        {
            var entity = EntityWrapperExtensions.QuickEntity("names")
                .AddRelation(1, "yes")
                .AddRelation(3, "no", "value");

            Assert.Empty(entity.Values);
            Assert.Equal(2, entity.Relations.Count);
            Assert.Equal(1, entity.Relations[0].entityId1);
            Assert.Equal(entity.id, entity.Relations[1].entityId2);
            Assert.Equal("yes", entity.Relations[0].type);
            Assert.Equal("value", entity.Relations[1].value);
        }

        protected EntityWrapper GetBasicWrapper()
        {
            return EntityWrapperExtensions.QuickEntity("aname")
                .AddValue("key1", "value1")
                .AddValue("key2", "value2")
                .AddRelation(1, "yes")
                .AddRelation(3, "no", "value");
        }

        [Fact]
        public void HasValue()
        {
            var entity = GetBasicWrapper();
            Assert.True(entity.HasValue("key1"));
            Assert.False(entity.HasValue("yes"));
        }

        [Fact]
        public void GetValue()
        {
            var entity = GetBasicWrapper();
            Assert.Equal("value2", entity.GetValue("key2"));
            Assert.ThrowsAny<InvalidOperationException>(() => entity.GetValue("no"));
        }

        [Fact]
        public void SetEntityNew()
        {
            var entity = GetBasicWrapper();
            entity.id = 5;
            entity.Values.ForEach(x => x.id = 99);
            entity.Relations.ForEach(x => x.id = 45);

            entity.SetEntityAsNew();

            //A new entity shoudl have no ids. there may be more things but this is all we're testing right now
            Assert.Equal(0, entity.id);
            Assert.All(entity.Values, x => Assert.Equal(0, x.id));
            Assert.All(entity.Relations, x => Assert.Equal(0, x.id));
        }
    }
}