using Xunit;
using contentapi.Services;
using contentapi.Models;
using AutoMapper;
using System;

namespace contentapi.test
{
    public class EntityServiceTest : TestBase
    {
        private EntityService CreateService()
        {
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<User,UserView>();
                cfg.CreateMap<UserView,User>();
            }); 
            var mapper = mapperConfig.CreateMapper();
            return new EntityService(mapper, new AccessService());
        }

        [Fact]
        public void SimpleUserSetEntity()
        {
            var service = CreateService();

            var user = new User();
            service.SetNewEntity(user);

            Assert.NotNull(user.Entity);
            Assert.NotNull(user.Entity.AccessList);
            Assert.NotNull(user.Entity.baseAllow);
            Assert.True(user.Entity.createDate >= DateTime.Now.AddSeconds(-60));
        }
    }
}