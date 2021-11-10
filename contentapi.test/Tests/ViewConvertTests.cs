using System;
using System.Collections.Generic;
using contentapi.Services.Constants;
using contentapi.Services.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;
using Xunit;

namespace contentapi.test
{
    public class ViewConverterTests : UnitTestBase
    {
        protected void FillBaseView(IBaseView view)
        {
            view.id = 5;
            view.createDate = DateTime.Now.Subtract(TimeSpan.FromDays(1));
        }

        protected void FillHistoricView(IEditView view)
        {
            FillBaseView(view);
            view.editUserId = 4;
            view.createUserId = 3;
            view.editDate = DateTime.Now;
        }

        protected void FillPermissionView(StandardView view)
        {
            FillHistoricView(view);
            view.parentId = 7;
            view.values.Add("key1", "value1");
            view.values.Add("keetooo", "velkdu");
            view.permissions.Add(0, "crud");
            view.permissions.Add(2, "cr");
        }

        [Fact]
        public void TestContentConvert()
        {
            var service = CreateService<ContentViewSource>();

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
            var service = CreateService<CategoryViewSource>();

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
            var service = CreateService<FileViewSource>();

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
            var service = CreateService<UserViewSource>();

            //Just some standard content view
            var view = new UserViewFull()
            {
                username = "random",
                password = "notactuallyapassword",
                salt = "thesearebytefieldsbro",
                avatar = 88,
                email = "email@ameila.com",
                registrationKey = "12345",
                special = "wowzers",
                hidelist = new List<long>() { 5, 8, 99 }
            };

            FillHistoricView(view);

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestCommentConvertSIMPLE()
        {
            var service = CreateService<CommentViewSource>();

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

        [Fact]
        public void TestActivityConvert()
        {
            var service = CreateService<ActivityViewSource>();

            //Just some standard content view
            var view = new ActivityView()
            {
                id = 99,
                contentId = 5,
                userId = 6,
                type = Keys.TypeNames[Keys.CategoryType],
                contentType = "pansu",
                date = DateTime.Now,
                extra = "yeah yeah ok",
                action = "!u"
            };

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        //[Fact]
        //public void TextActivityRegression()
        //{
        //    var entity = new Entity()
        //    {
        //        id = 5,
        //        type = "tc@page.resource"
        //    };

        //    //Nobody is supposed to create activity views directly
        //    var relation = service.MakeActivity(entity, 55, Keys.UpdateAction, "something");
            //Assert.Equal(view.type, "tc");

        //}


        [Fact]
        public void TestWatchConvert()
        {
            var service = CreateService<WatchViewSource>();

            //Just some standard content view
            var view = new WatchView()
            {
                id = 99,
                contentId = 5,
                userId = 6,
                lastNotificationId = 89,
                createDate = DateTime.Now
            };

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestModuleConvert()
        {
            var service = CreateService<ModuleViewSource>();

            //Just some standard content view
            var view = new ModuleView()
            {
                id = 99,
                name = "test",
                code = "this is lua\nprobably",
                createUserId = 4,
                editUserId = 4,
                createDate = DateTime.Now,
                editDate = DateTime.Now.AddDays(1)
            };

            view.values["wow"] = "yes";
            view.values["no"] = "come on";

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }

        [Fact]
        public void TestBanConvert()
        {
            var service = CreateService<BanViewSource>();

            //var tailoredDate = (new DateTime((DateTime.Now.AddDays(5).Ticks / 10000000) * 10000000)).ToUniversalTime(); //, DateTime.Now.Kind); //DateTime.Now.AddDays(5);
            var tailoredDate = new DateTime((DateTime.Now.AddDays(5).Ticks / 10000000) * 10000000);
            //tailoredDate.Ticks = tailoredDate.Ticks;

            //Just some standard content view
            var view = new BanView()
            {
                id = 99,
                createUserId = 88,
                bannedUserId = 76,
                expireDate = tailoredDate,
                message = "Wowwee zowwoieeiy",
                type = BanType.@public,
                createDate = DateTime.Now
            };

            var temp = service.FromView(view);
            var view2 = service.ToView(temp);

            Assert.Equal(view, view2);
        }
    }
}