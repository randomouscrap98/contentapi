using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;
using Xunit;

namespace contentapi.test
{
    public class ContentControllerTest : ControllerTestBase<ContentController>
    {
        public ControllerInstance<ContentController> baseInstance;

        public ContentControllerTest()
        {
            baseInstance = GetInstance(true);
        }

        protected CategoryView CreateCategoryView(string baseAccess = "CRUD")
        {
            return new CategoryView()
            {
                name = "c_" + UniqueSection(),
                baseAccess = baseAccess,
                type = "justInCase"
            };
        }

        protected ContentView CreateContentView(long categoryId, string baseAccess = "CRUD")
        {
            return new ContentView()
            {
                categoryId = categoryId,
                title = "t_" + UniqueSection(),
                content = "Just whatever",
                baseAccess = baseAccess
            };
        }

        protected void CompareContent(ContentView original, ContentView posted)
        {
            Assert.True(posted.id > 0);
            Assert.Equal(original.content, posted.content);
            Assert.Equal(original.baseAccess, posted.baseAccess);
            Assert.Equal(original.categoryId, posted.categoryId);
        }

        protected CategoryView CreateCategory(CategoryView view = null, ControllerInstance<ContentController> instance = null)
        {
            instance = instance ?? GetInstance(true, Role.SiteAdministrator);
            view = view ?? CreateCategoryView();
            var controller = instance.GetExtService<CategoriesController>();
            var result = controller.Post(view).Result;
            Assert.True(IsSuccessRequest(result));
            return result.Value;
        }

        [Fact]
        public void TestSimpleContentCreate()
        {
            //Create a category to put content into (assume it creates a good category)
            var category = CreateCategory();
            var content = CreateContentView(category.id);

            //Now just... post a content!
            var result = baseInstance.Controller.Post(content).Result;
            Assert.True(IsSuccessRequest(result));
            CompareContent(content, result.Value);
        }

        [Fact]
        public void TestSimpleContentCantCreate()
        {
            //This category has no create permission
            var category = CreateCategory(CreateCategoryView("RUD"));
            var content = CreateContentView(category.id);
            var result = baseInstance.Controller.Post(content).Result;
            Assert.False(IsSuccessRequest(result)); //Make sure we can't post
            //Also make sure the content isn't there (we SHOULD have an empty content list for debugging...)
            var contents = baseInstance.QueryService.GetCollectionFromResult<ContentView>(baseInstance.Controller.Get(new CollectionQuery()).Result.Value);
            //Assert.False(contents.Any(x => x.title == content.title));
            Assert.DoesNotContain(contents, x => x.title == content.title);
        }
    }
}