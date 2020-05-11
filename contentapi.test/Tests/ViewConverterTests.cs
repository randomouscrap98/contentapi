using System;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    public class ViewConverterTests : UnitTestBase
    {
        protected void FillBaseView(BaseView view)
        {
            view.id = 5;
            view.createDate = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        }

        protected void FillHistoricView(BaseEntityView view)
        {
            FillBaseView(view);
            view.editUserId = 4;
            view.createUserId = 3;
            view.editDate = DateTime.Now;
        }

        protected void FillPermissionView(BasePermissionView view)
        {
            FillHistoricView(view);
            view.parentId = 7;
            view.values.Add("key1", "value1");
            view.values.Add("keetooo", "velkdu");
            view.permissions.Add("0", "crud");
            view.permissions.Add("2", "cr");
        }

        [Fact]
        public void TestContentConvert()
        {
            var service = CreateService<ContentViewConverter>();

            //Just some standard content view
            var view = new ContentView()
            {
                content = "wow it's a content",
                name = "thecontent",
                type = "kek"
            };

            FillPermissionView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }
    }
}