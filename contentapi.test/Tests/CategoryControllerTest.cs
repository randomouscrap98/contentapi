using contentapi.Controllers;
using contentapi.Models;
using Xunit;

namespace contentapi.test
{
    public class CategoryControllerTest : ControllerTestBase<CategoriesController>
    {
        public CategoryView QuickCategory(string name)
        {
            return new CategoryView()
            {
                parentId = null,
                name = name,
                description = null,
                type = "default",
                baseAccess = "CR"
            };
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestCreateCategoryUnauthorized(bool loggedIn)
        {
            //By default, our user should not be able to post categories
            var instance = GetInstance(loggedIn);
            var category = QuickCategory("My new category");
            var result = instance.Controller.Post(category).Result;
            Assert.True(IsNotAuthorized(result.Result));
        }
    }
}