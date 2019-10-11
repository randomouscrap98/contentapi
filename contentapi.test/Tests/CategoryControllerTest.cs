using contentapi.Controllers;
using contentapi.Models;
using Xunit;

namespace contentapi.test
{
    public class CategoryControllerTest : ControllerTestBase<CategoriesController>
    {
        public CategoryControllerTest(TestControllerContext context) : base(context) {}

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

        [Fact]
        public void TestCreateCategoryUnauthorized()
        {
            //By default, our user should not be able to post categories
            context.Login();
            var category = QuickCategory("My new category");
            var result = controller.Post(category).Result;
            Assert.True(context.IsNotAuthorized(result.Result));
        }
    }
}