using System;
using System.Linq;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;
using Xunit;

namespace contentapi.test
{
    public class HistoryServiceTests : UnitTestBase //: ServiceTestBase<HistoryService>
    {
        IEntityProvider provider;
        HistoryService service;

        public HistoryServiceTests()
        {
            provider = CreateService<IEntityProvider>();
            service = new HistoryService(CreateService<ILogger<HistoryService>>(), CreateService<Keys>(), provider,
                CreateService<IActivityService>());
        }

        [Fact]
        public void SimpleInsert()
        {
            //Can it at least be inserted without throwing exceptions??
            var package = NewPackage();
            service.InsertWithHistoryAsync(package, 1).Wait();

            Assert.True(package.Entity.id > 0);

            //Also there should be no revisions for this.
            var revisions = service.GetRevisionIdsAsync(package.Entity.id).Result;
            Assert.Empty(revisions);
        }

        [Fact]
        public void SimpleMultiInsert()
        {
            //Can it at least be inserted without throwing exceptions??
            var package = NewPackage();
            service.InsertWithHistoryAsync(package, 1).Wait();

            var package2 = NewPackage();
            service.InsertWithHistoryAsync(package2, 1).Wait();

            //Also there should be no revisions for this.
            var revisions = service.GetRevisionIdsAsync(package.Entity.id).Result;
            Assert.Empty(revisions);
            revisions = service.GetRevisionIdsAsync(package2.Entity.id).Result;
            Assert.Empty(revisions);
        }

        protected EntityPackage SimplePackage()
        {
            return (new EntityPackage()
            {
                Entity = new Entity()
                {
                    name = "theName",
                    content = "the content",
                    type = "something",
                    createDate = DateTime.Now
                }
            }).Add(new EntityRelation()
            {
                entityId1 = 55,
                type = "relate",
                createDate = DateTime.Now
            }).Add(new EntityValue()
            {
                entityId = 0,
                key = "somevalue",
                value = "aha",
                createDate = DateTime.Now
            });
        }

        protected EntityPackage SetLikeNew(EntityPackage package)
        {
            package.Entity.id = 0;
            package.Values.ForEach(x => x.id = 0);
            package.Relations.ForEach(x => x.id = 0);
            return package;
        }

        /// <summary>
        /// Stupid datetime bull...
        /// </summary>
        /// <param name="one"></param>
        protected void FixEntity(EntityBase fix)//, EntityBase two)
        {
            fix.createDate = fix.createDateProper(); //Set create date to proper so everything is aligned
        }

        protected void FixPackage(EntityPackage package)
        {
            FixEntity(package.Entity);
            package.Values.ForEach(x => FixEntity(x));
            package.Relations.ForEach(x => FixEntity(x));
        }

        protected void AssertFixedEqual(EntityPackage one, EntityPackage two)
        {
            FixPackage(one);
            FixPackage(two);
            Assert.Equal(one, two);
        }

        protected void AssertHistoryEqual(EntityPackage origin, EntityPackage history)
        {
            var originCopy = SetLikeNew(new EntityPackage(origin));
            var historyCopy = SetLikeNew(new EntityPackage(history));

            var compare = service.ConvertHistoryToUpdate(historyCopy);

            AssertFixedEqual(originCopy, compare);

            //Assert.Equal(originCopy.Entity, historyCopy.Entity);

            //foreach(var value in originCopy.Values)
            //    Assert.Contains(value, historyCopy.Values);

            //foreach(var relation in originCopy.Relations)
            //    Assert.Contains(relation, historyCopy.Relations);
        }

        [Fact]
        public void FullUpdate() //Lots of tests with updating
        {
            var package = SimplePackage();
            var originaPackageCopy = new EntityPackage(package);

            service.InsertWithHistoryAsync(package, 1).Wait();

            var firstInsertedPackage = new EntityPackage(package);

            package.Relations.First().value = "Something NEW";
            package.Values.First().value = "aha MORE";

            service.UpdateWithHistoryAsync(package, 1).Wait();

            //This shouldn't be "new", it should be the same as before
            Assert.Equal(firstInsertedPackage.Entity.id, package.Entity.id);

            var revisions = service.GetRevisionIdsAsync(package.Entity.id).Result;
            Assert.Single(revisions);
            Assert.NotEqual(package.Entity.id, revisions.First());

            //Ensure the CURRENT package we pulled out is EXACTLY the same (minus date kind)
            var currentPackage = provider.FindByIdAsync(package.Entity.id).Result;
            AssertFixedEqual(package, currentPackage);

            //Ensure the package from history is EXACTLY the same as the one before sans ids (set all to 0)
            var revisionPackage = provider.FindByIdAsync(revisions.First()).Result;
            var likeUpdate = service.ConvertHistoryToUpdate(revisionPackage);
            AssertFixedEqual(firstInsertedPackage, likeUpdate);

            //AssertHistoryEqual(firstInsertedPackage, revisionPackage);
        }
    }
}