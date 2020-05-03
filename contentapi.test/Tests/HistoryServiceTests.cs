using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;
using Xunit;

//I seriously can't stand XUnit and their pedantic bullshit
#pragma warning disable xUnit2012

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

        ///// <summary>
        ///// Stupid datetime bull...
        ///// </summary>
        ///// <param name="one"></param>
        //protected void FixEntity(EntityBase fix)//, EntityBase two)
        //{
        //    fix.createDate = fix.createDateProper(); //Set create date to proper so everything is aligned
        //}

        //protected void FixPackage(EntityPackage package)
        //{
        //    FixEntity(package.Entity);
        //    package.Values.ForEach(x => FixEntity(x));
        //    package.Relations.ForEach(x => FixEntity(x));
        //}

        //protected void AssertFixedEqual(EntityPackage one, EntityPackage two)
        //{
        //    FixPackage(one);
        //    FixPackage(two);
        //    Assert.Equal(one, two);
        //}

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
            Assert.Equal(package, currentPackage);

            //Ensure the package from history is EXACTLY the same as the one before sans ids (set all to 0)
            var revisionPackage = provider.FindByIdAsync(revisions.First()).Result;
            var likeUpdate = service.ConvertHistoryToUpdate(revisionPackage);
            Assert.Equal(firstInsertedPackage, likeUpdate);
        }

        [Fact]
        public void ManyUpdates()
        {
            const int updateCount = 10;
            var package = SimplePackage();
            var originaPackageCopy = new EntityPackage(package);

            service.InsertWithHistoryAsync(package, 1).Wait();

            //var firstInsertedPackage = new EntityPackage(package);

            var updates = new List<EntityPackage>();

            for(var i = 0; i < updateCount; i++)
            {
                //Add updates FIRST because technically the revisions will show the history, which will be all but the last. So the first
                //"insert" (before update) will be the first history revision
                updates.Add(new EntityPackage(package));

                package.Entity.name += "a";
                package.Entity.type += "t";

                package.Relations.First().value = $"updaterelation:{i}";
                package.Values.First().value = $"updatevalue:{i}";
                package.Add(new EntityRelation()
                {
                    entityId1 = (i + 1) * 3, entityId2 = 0, createDate = DateTime.Now,
                    type = $"newrelate{i}",
                }).Add(new EntityValue()
                {
                    entityId = 0, createDate = DateTime.Now,
                    key = $"newkey{i}",
                    value = $"newvalue{i}",
                });

                service.UpdateWithHistoryAsync(package, 1).Wait();
            }

            //This shouldn't be "new", it should be the same as before (even after alll those inserts)
            Assert.Equal(updates[0].Entity.id, package.Entity.id);

            //Ensure the CURRENT package we pulled out is EXACTLY the same (minus date kind)
            var currentPackage = provider.FindByIdAsync(package.Entity.id).Result;
            Assert.Equal(package, currentPackage);

            var revisions = service.GetRevisionIdsAsync(package.Entity.id).Result.OrderBy(x => x).ToList();
            Assert.Equal(10, revisions.Count());
            Assert.False(revisions.Any(x => x == package.Entity.id));

            for(int i = 0; i < updateCount; i++)
            {
                //Ensure the package from history is EXACTLY the same as the one before sans ids (set all to 0)
                var revisionPackage = provider.FindByIdAsync(revisions[i]).Result;
                var likeUpdate = service.ConvertHistoryToUpdate(revisionPackage);
                Assert.Equal(updates[i], likeUpdate);

                if(i > 0)   //I'm paranoid
                    Assert.NotEqual(updates[i - 1], likeUpdate);
            }
        }

        //[Fact]
        //public void SimpleDelete() //Lots of tests with updating
        //{
        //    var package = SimplePackage();
        //    var originaPackageCopy = new EntityPackage(package);

        //    service.InsertWithHistoryAsync(package, 1).Wait();

        //    var currentPackage = provider.FindByIdAsync(package.Entity.id).Result;
        //    Assert.Equal(package.Entity.id, currentPackage.Entity.id);

        //    service.DeleteWithHistoryAsync(currentPackage, 1).Wait();

        //    currentPackage = provider.FindByIdAsync(package.Entity.id).Result;
        //    Assert.Null(currentPackage);
        //}
    }
}