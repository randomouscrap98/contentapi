using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Controllers;
using contentapi.Models;
using Xunit;

namespace contentapi.test
{
    public class CategoryControllerTest : ControllerTestBase<CategoriesController>
    {
        public const Role CategoryRole = Role.SiteAdministrator;

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

        public CategoryView CreateUniqueCategory(ControllerInstance<CategoriesController> instance, Action<CategoryView> alterCategory = null)
        {
            var category = QuickCategory("Category_" + UniqueSection());
            if(alterCategory != null)
                alterCategory.Invoke(category);
            var result = instance.Controller.Post(category).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.name == category.name);
            return result.Value;
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

        [Fact]
        public void TestCreateCategory()
        {
            var instance = GetInstance(true, CategoryRole);
            var category = CreateUniqueCategory(instance); //This ALSO checks (kinda bad)
        }

        [Fact]
        public void TestGetCategorySingle()
        {
            var instance = GetInstance(true, CategoryRole);
            var category = CreateUniqueCategory(instance);
            var result = instance.Controller.GetSingle(category.id).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.id == category.id);
            Assert.True(result.Value.name == category.name);
        }

        [Fact]
        public void TestGetCategoryList()
        {
            var instance = GetInstance(true, CategoryRole);
            var category = CreateUniqueCategory(instance);
            var category2 = CreateUniqueCategory(instance);
            var result = instance.Controller.Get(new Services.CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            var collection = instance.QueryService.GetCollectionFromResult<CategoryView>(result.Value);
            Assert.True(collection.Count() >= 2);
            Assert.Contains(collection, x => x.id == category.id);
            Assert.Contains(collection, x => x.id == category2.id);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestCantReadCategorySingle(bool loggedIn)
        {
            var instance = GetInstance(true, CategoryRole);
            var category = CreateUniqueCategory(instance, (c) => c.baseAccess = "CUD");

            var instance2 = GetInstance(loggedIn);
            var result = instance2.Controller.GetSingle(category.id).Result;
            Assert.False(IsSuccessRequest(result));
            Assert.Null(result.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestCantReadCategoryGet(bool loggedIn)
        {
            var instance = GetInstance(true, CategoryRole);
            var category = CreateUniqueCategory(instance, (c) => c.baseAccess = "CUD");

            var instance2 = GetInstance(loggedIn);
            var result = instance2.Controller.Get(new Services.CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            var categories = instance.QueryService.GetCollectionFromResult<CategoryView>(result.Value);
            Assert.DoesNotContain(categories, (c) => c.id == category.id);
        }

        [Fact]
        public void TestNoCategoriesNullParent()
        {
            var instance = GetInstance(true, CategoryRole);
            var result = instance.Controller.Get(new CategoryQuery(){parentId = -1}).Result;
            Assert.True(IsSuccessRequest(result));
            var categories = instance.QueryService.GetCollectionFromResult<CategoryView>(result.Value);
            Assert.Empty(categories);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public void ComprehensiveParentFilter(bool useParent, int extraCategories)
        {
            var instance = GetInstance(true, CategoryRole);

            //Our parent to hold other junk
            var parent = CreateUniqueCategory(instance);
            var junkCategories = new List<CategoryView>();

            //Some fake categories outside us to make sure everything is hunky-dory
            for(int i = 0; i < 5; i++)
                junkCategories.Add(CreateUniqueCategory(instance, (c) => c.parentId = parent.id));
            
            long parentId = -1;
            var myCategories = new List<CategoryView>();

            if(useParent)
            {
                parentId = (long)CreateUniqueCategory(instance).id;

                for(int i = 0; i < extraCategories; i++)
                    myCategories.Add(CreateUniqueCategory(instance, (c) => c.parentId = parentId));
            }
            else
            {
                myCategories.Add(parent);

                for(int i = 0; i < extraCategories; i++)
                    myCategories.Add(CreateUniqueCategory(instance));
            }

            var result = instance.Controller.Get(new CategoryQuery(){parentId = parentId}).Result;
            Assert.True(IsSuccessRequest(result));
            var categories = instance.QueryService.GetCollectionFromResult<CategoryView>(result.Value);
            Assert.Equal(myCategories.Count(), categories.Count());

            if(myCategories.Count() > 0)
                Assert.True(categories.Select(x => x.id).OrderBy(x => x).SequenceEqual(myCategories.Select(x => x.id).OrderBy(x => x)));
        }
    }
}