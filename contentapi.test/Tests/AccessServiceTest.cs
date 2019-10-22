using Xunit;
using contentapi.Services;
using contentapi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.test
{
    public class AccessServiceTest
    {
        private AccessService CreateService()
        {
            return new AccessService();
        }

        private EntityAction GetAllActions()
        {
            return EntityAction.Create | EntityAction.Read | EntityAction.Update | EntityAction.Delete;
        }

        private Entity GetDefaultEntity()
        {
            return new Entity() {AccessList = new List<EntityAccess>()};
        }

        [Theory]
        [InlineData("C", EntityAction.Create)]
        [InlineData("R", EntityAction.Read)]
        [InlineData("U", EntityAction.Update)]
        [InlineData("D", EntityAction.Delete)]
        [InlineData("CD", EntityAction.Create | EntityAction.Delete)]
        [InlineData("DUR", EntityAction.Delete | EntityAction.Update | EntityAction.Read)]
        [InlineData("RDC", EntityAction.Read | EntityAction.Delete | EntityAction.Create)]
        public void CheckStringToAccess(string format, EntityAction action)
        {
            var service = CreateService();
            Assert.True(service.StringToAccess(format) == action);
        }

        //This test depends on the ORDER of the CRUD! If that's not what you want, fix it!
        [Theory]
        [InlineData("C", EntityAction.Create)]
        [InlineData("R", EntityAction.Read)]
        [InlineData("U", EntityAction.Update)]
        [InlineData("D", EntityAction.Delete)]
        [InlineData("CD", EntityAction.Create | EntityAction.Delete)]
        [InlineData("RUD", EntityAction.Delete | EntityAction.Update | EntityAction.Read)]
        [InlineData("CRD", EntityAction.Read | EntityAction.Delete | EntityAction.Create)]
        public void CheckAccessToString(string format, EntityAction action)
        {
            var service = CreateService();
            Assert.True(service.AccessToString(action) == format);
        }

        [Theory]
        [InlineData("c")]
        [InlineData("RUUD")]
        [InlineData("CC")]
        [InlineData("Z")]
        [InlineData("ABC")]
        [InlineData("CRUDD")]
        public void CheckAccessFormatFalse(string format)
        {
            var service = CreateService();
            Assert.ThrowsAny<Exception>(new Action(() => service.StringToAccess(format)));
        }

        private void CanUserCRUDBase(int id, Entity model)
        {
            var service = CreateService();
            var user = new UserEntity() {entityId = id};

            Assert.True(service.CanCreate(model, user));
            Assert.True(service.CanRead(model, user));
            Assert.True(service.CanUpdate(model, user));
            Assert.True(service.CanDelete(model, user));
        }

        [Fact]
        private void CantUserCRUD()
        {
            var service = CreateService();
            var user = new UserEntity() {entityId = 5};
            var model = new Entity() {};

            Assert.False(service.CanCreate(model, user));
            Assert.False(service.CanRead(model, user));
            Assert.False(service.CanUpdate(model, user));
            Assert.False(service.CanDelete(model, user));
        }

        [Fact]
        public void CanUserCRUD()
        {
            CanUserCRUDBase(5, new Entity() {baseAllow = GetAllActions() } );
        }

        [Fact]
        public void CanUserCRUDSingle()
        {
            var mega = GetDefaultEntity();
            mega.AccessList.Add(new EntityAccess() { userId = 5, allow = GetAllActions()});
            Assert.True(mega.AccessList.Count > 0);
            CanUserCRUDBase(5, mega);
        }

        [Fact]
        public void TestFillEntityAccess()
        {
            var service = CreateService();
            var view = new EntityView()
            {
                baseAccess = "CR",
                accessList = new Dictionary<long, string>() { {3, "UD"}, {4, "D"}}
            };
            var entity = GetDefaultEntity();
            service.FillEntityAccess(entity, view);
            Assert.True(entity.baseAllow == (EntityAction.Create | EntityAction.Read));
            Assert.True(entity.AccessList.First(x => x.userId == 3).allow == (EntityAction.Update | EntityAction.Delete));
            Assert.True(entity.AccessList.First(x => x.userId == 4).allow == EntityAction.Delete);
        }

        [Fact]
        public void TestFillViewAccess()
        {
            var service = CreateService();
            var entity = new Entity()
            {
                baseAllow = EntityAction.Create | EntityAction.Read,
                AccessList = new List<EntityAccess>() {
                    new EntityAccess() { userId = 3, allow = EntityAction.Update | EntityAction.Delete},
                    new EntityAccess() { userId = 4, allow = EntityAction.Delete}
                }
            };
            var view = new EntityView();
            service.FillViewAccess(view, entity);
            Assert.True(view.baseAccess == "CR");
            Assert.True(view.accessList[3] == "UD");
            Assert.True(view.accessList[4] == "D");
        }
    }
}
