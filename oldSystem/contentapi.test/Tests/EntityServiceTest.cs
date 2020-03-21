using Xunit;
using contentapi.Services;
using contentapi.Models;
using AutoMapper;
using System;
using System.Linq;
using System.Collections.Generic;

namespace contentapi.test
{
    public class EntityServiceTest : TestBase
    {
        private EntityService CreateService()
        {
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<UserEntity,UserView>();
                cfg.CreateMap<UserView,UserEntity>();
            }); 
            var mapper = mapperConfig.CreateMapper();
            return new EntityService(mapper, new AccessService(new Configs.AccessConfig()));
        }

        private UserView GetSimpleUserView()
        {
            return new UserView()
            {
                username = "Wow",
                role = "SiteAdministrator"
            };
        }

        private UserEntity GetSimpleUser()
        {
            return new UserEntity()
            {
                Entity = new Entity()
                {
                    createDate = DateTime.Now,
                    id = 5,
                    status = 0,
                    AccessList = new List<EntityAccess>()
                },
                entityId = 5,
                username = "Wow",
                role = Role.SiteAdministrator
            };
        }

        private void TestEntity(EntityChild entity)
        {
            Assert.NotNull(entity.Entity);
            Assert.NotNull(entity.Entity.AccessList);
            //Assert.NotEqual(, entity.Entity.baseAllow);
            Assert.True(entity.Entity.createDate >= DateTime.Now.AddSeconds(-60));
        }

        private void TestUser(UserEntity user, UserView view)
        {
            Assert.Equal(view.username, user.username);
            Assert.Equal(Role.SiteAdministrator, user.role);
        }

        private void TestView(UserView view, UserEntity user)
        {
            Assert.Equal(user.username, view.username);
            Assert.Equal("SiteAdministrator", view.role);
            Assert.Equal(user.Entity.createDate, view.createDate);
            Assert.Equal(user.Entity.id, view.id);
            Assert.Equal(user.entityId, view.id);
        }

        [Fact]
        public void SimpleUserSetEntity()
        {
            var service = CreateService();

            var user = new UserEntity();
            service.SetNewEntity(user);

            TestEntity(user);
        }

        [Fact]
        public void SimpleUserViewConvert()
        {
            var service = CreateService();

            var view = GetSimpleUserView();
            var user = service.ConvertFromView<UserEntity, UserView>(view);

            TestEntity(user);
            TestUser(user, view);
        }

        [Fact]
        public void ComplexUserViewConvert()
        {
            var service = CreateService();

            var view = GetSimpleUserView();
            view.baseAccess = "CR";
            view.accessList = new System.Collections.Generic.Dictionary<string, string>()
            {
                { "5", "UD" },
                { "6", "U" }
            };

            var user = service.ConvertFromView<UserEntity, UserView>(view);

            TestEntity(user);
            TestUser(user, view);
            Assert.Equal(EntityAction.Create | EntityAction.Read, user.Entity.baseAllow);
            Assert.Equal(EntityAction.Update | EntityAction.Delete, user.Entity.AccessList.First(x => x.userId == 5).allow);
            Assert.Equal(EntityAction.Update, user.Entity.AccessList.First(x => x.userId == 6).allow);
        }

        [Fact]
        public void SimpleUserConvert()
        {
            var service = CreateService();

            var user = GetSimpleUser();

            var view = service.ConvertFromEntity<UserEntity, UserView>(user);

            TestView(view, user);
        }

        [Fact]
        public void ComplexUserConvert()
        {
            var service = CreateService();

            var user = GetSimpleUser();

            user.Entity.baseAllow = EntityAction.Create | EntityAction.Delete;
            user.Entity.AccessList = new List<EntityAccess>() 
            { 
                new EntityAccess() {userId = 6, allow = EntityAction.Update},
                new EntityAccess() {userId = 7, allow = EntityAction.Read | EntityAction.Update}
            };

            var view = service.ConvertFromEntity<UserEntity, UserView>(user);

            TestView(view, user);
            Assert.Equal("CD", view.baseAccess);
            Assert.Equal("U", view.accessList["6"]);
            Assert.Equal("RU", view.accessList["7"]);
        }
    }
}