using System;
using Xunit;
using System.IO;
using contentapi.Services;
using contentapi.Models;

namespace contentapi.test
{
    public class AccessServiceTest
    {
        private AccessService CreateService()
        {
            return new AccessService();
        }

        [Theory]
        [InlineData("C")]
        [InlineData("R")]
        [InlineData("U")]
        [InlineData("D")]
        [InlineData("CD")]
        [InlineData("DUR")]
        [InlineData("RDC")]
        public void CheckAccessFormat(string format)
        {
            var service = CreateService();
            Assert.True(service.CheckAccessFormat(format));
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
            Assert.False(service.CheckAccessFormat(format));
        }

        private void CanUserCRUDBase(int id, IGenericAccessModel model)
        {
            var service = CreateService();
            var user = new User() {id = id};

            Assert.True(service.CanCreate(model, user));
            Assert.True(service.CanRead(model, user));
            Assert.True(service.CanUpdate(model, user));
            Assert.True(service.CanDelete(model, user));
        }

        [Fact]
        public void CanUserCRUD()
        {
            CanUserCRUDBase(5, new GenericAccessModel() {baseAccess = "CRUD"});
        }

        [Fact]
        public void CanUserCRUDSingle()
        {
            var mega = new GenericAccessModel();
            mega.GenericAccessList.Add(new GenericSingleAccess() { userId = 5, access = "CRUD"});
            Assert.True(mega.GenericAccessList.Count > 0);
            CanUserCRUDBase(5, mega);
        }
    }
}
