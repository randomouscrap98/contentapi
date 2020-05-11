using System;
using contentapi.Services.Constants;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;
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

            view.keywords.AddRange(new[] {"this", "is", "spret"});

            FillPermissionView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestCategoryConvert()
        {
            var service = CreateService<CategoryViewConverter>();

            //Just some standard content view
            var view = new CategoryView()
            {
                description = "wow it's a category",
                name = "somethingtosee"
            };

            view.localSupers.AddRange(new[] { 99L, 100 });

            FillPermissionView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestFileConvert()
        {
            var service = CreateService<FileViewConverter>();

            //Just some standard content view
            var view = new FileView()
            {
                fileType = "sys/wow",
                name = "filesHaveNamesQuestionMark"
            };

            FillPermissionView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestUserConvert()
        {
            var service = CreateService<UserViewConverter>();

            //Just some standard content view
            var view = new UserViewFull()
            {
                username = "random",
                password = "notactuallyapassword",
                salt = "thesearebytefieldsbro",
                avatar = 88,
                email = "email@ameila.com",
                registrationKey = "12345"
            };

            FillHistoricView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestCommentConvertSIMPLE()
        {
            var service = CreateService<CommentViewConverter>();

            //Do it the OTHER way since we're only testing the "simple" portion.
            var relation = new EntityRelation()
            {
                entityId1 = 4,
                entityId2 = -6,
                value = "wow",
                type = Keys.CommentHack,
                createDate = DateTime.Now
            };

            var temp = service.ToViewSimple(relation);
            var relation2 = service.FromViewSimple(temp);

            Assert.Equal(relation, relation2);
        }
    }
}