using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    //This will eventually change to the permission service test. Although content will have something different
    //I suppose... or perhaps just more tests :)
    public class ContentViewServiceTest : PermissionServiceTestBase<ContentViewService, ContentView, ContentSearch>
    {
        protected void AssertSupersEqual(IEnumerable<long> one, IEnumerable<long> two)
        {
            Assert.Equal(new HashSet<long>(one), new HashSet<long>(two));
        }

        public ContentViewServiceTest() : base()
        {
            service.SetupAsync().Wait();
        }


        //Get the boring stuff out of the way.
        [Fact] public override void SimpleEmptyCanUser() { base.SimpleEmptyCanUser(); }
        [Fact] public override void SimpleEmptyRead() { base.SimpleEmptyRead(); }
        [Fact] public override void SimpleOwnerInsert() { base.SimpleOwnerInsert(); }
        [Fact] public override void SimpleOwnerMultiInsert() { base.SimpleOwnerMultiInsert(); }
        [Fact] public override void SimpleOwnerUpdate() { base.SimpleOwnerUpdate(); }
        [Fact] public override void SimpleOwnerDelete() { base.SimpleOwnerDelete(); }
        [Fact] public override void SimpleNoParentSuper() { base.SimpleNoParentSuper(); }

        [Theory]
        [InlineData("c", 0, "", false, false)]  //First four are standard user no perm disallow
        [InlineData("r", 0, "", false, false)]
        [InlineData("u", 0, "", false, false)]
        [InlineData("d", 0, "", false, false)]
        [InlineData("c", 0, "", true, true)]    //next are super users, not allowed to read but can do all else
        [InlineData("r", 0, "", true, false)]
        [InlineData("u", 0, "", true, true)]
        [InlineData("d", 0, "", true, true)]
        [InlineData("c", 0, "c", false, true)]  //Now if you are in the default group, you can do it all
        [InlineData("r", 0, "r", false, true)]
        [InlineData("u", 0, "u", false, true)]
        [InlineData("d", 0, "d", false, true)]
        [InlineData("c", 2, "c", false, true)]  //And then for the particular user.
        [InlineData("r", 2, "r", false, true)]
        [InlineData("u", 2, "u", false, true)]
        [InlineData("d", 2, "d", false, true)]
        public override void PermissionGeneral(string action, long permUser, string permValue, bool super, bool allowed)
        {
            base.PermissionGeneral(action, permUser, permValue, super, allowed);
        }

        [Theory]
        [InlineData("c", true, true)] 
        [InlineData("r", true, false)]
        [InlineData("u", true, true)]
        [InlineData("d", true, true)]
        [InlineData("c", false, false)] 
        [InlineData("r", false, false)]
        [InlineData("u", false, false)]
        [InlineData("d", false, false)]
        public void LocalSuperPermission(string action, bool localSuper, bool allowed)
        {
            var isAction = new Func<string, bool>((s) => action.ToLower().StartsWith(s.ToLower()));

            var tryAction = new Action<Action>((a) =>
            {
                try 
                {   
                    a(); 
                    Assert.True(allowed);
                }
                catch(AuthorizationException) 
                { 
                    Assert.False(allowed); 
                }
                catch(AggregateException ex) when (ex.InnerException is AuthorizationException) 
                { 
                    Assert.False(allowed); 
                }
            });

            //var contentOwner = CreateFakeUserAsync().Result;
            var requester = CreateFakeUserAsync().Result;
            var system = new Requester() { system = true };

            //Need to create a category with no supers.
            var category = new CategoryView() { name = "whatever" };
            if(localSuper) category.localSupers.Add(requester.userId);
            var categoryService = CreateService<CategoryViewService>();
            var writtenCategory = categoryService.WriteAsync(category, system).Result;

            //Need to reset content to get new local supers. they are cached per "request"
            service.SetupAsync().Wait();

            //Now we need to do a basic insert for a content.
            var content = new ContentView() { name = "myContent", parentId = writtenCategory.id };
            ContentView writtenContent = null;

            //If we're TESTING create, stop here.
            if(isAction("c"))
            {
                tryAction(() => writtenContent = service.WriteAsync(content, requester).Result);
            }
            else
            {
                writtenContent = service.WriteAsync(content, system).Result;

                //Now try to do the OTHEr things on it.
                if(isAction("r"))
                {
                    var result = FindByIdAsync(writtenContent.id, requester).Result;

                    if(allowed)
                        Assert.NotNull(result);
                    else
                        Assert.Null(result);
                }
                else if(isAction("u"))
                {
                    tryAction(() => service.WriteAsync(writtenContent, requester).Wait());
                }
                else if(isAction("d"))
                {
                    tryAction(() => service.DeleteAsync(writtenContent.id, requester).Wait());
                }
                else
                {
                    throw new InvalidOperationException($"Unknown action type: {action}");
                }
            }
        }

        [Fact]
        public void TestSingleSuperCache()
        {
            var supers = service.GetAllSupers(new[] {new CategoryView() 
            { 
                id = 1,
                parentId = -1, 
                localSupers = new List<long> { 5, 6, 7 } 
            }});

            Assert.Single(supers);
            Assert.True(supers.ContainsKey(1));
            AssertSupersEqual(new[] {5L,6,7}, supers[1]);
        }

        [Fact]
        public void TestSeparateSuperCache()
        {
            var supers = service.GetAllSupers(new[] 
            {
                new CategoryView() 
                { 
                    id = 1,
                    parentId = -1, 
                    localSupers = new List<long> { 5, 6, 7 } 
                },
                new CategoryView() 
                { 
                    id = 2,
                    parentId = -1, 
                    localSupers = new List<long> { 8, 9 } 
                }
            });

            Assert.Equal(2, supers.Count);
            Assert.True(supers.ContainsKey(1));
            Assert.True(supers.ContainsKey(2));
            AssertSupersEqual(new[] {5L,6,7}, supers[1]);
            AssertSupersEqual(new[] {8L,9}, supers[2]);
        }

        [Fact]
        public void TestSimpleParentSuperCache()
        {
            var supers = service.GetAllSupers(new[] 
            {
                new CategoryView() 
                { 
                    id = 1,
                    parentId = -1, 
                    localSupers = new List<long> { 5, 6, 7 } 
                },
                new CategoryView() 
                { 
                    id = 2,
                    parentId = 1, 
                    localSupers = new List<long> { 8, 9 } 
                }
            });

            Assert.Equal(2, supers.Count);
            Assert.True(supers.ContainsKey(1));
            Assert.True(supers.ContainsKey(2));
            AssertSupersEqual(new[] {5L,6,7}, supers[1]);
            AssertSupersEqual(new[] {5L, 6, 7, 8L,9}, supers[2]); //2 should inherit from 1
        }

        [Fact]
        public void TestComplexSuperCache()
        {
            var supers = service.GetAllSupers(new[] 
            {
                new CategoryView() 
                { 
                    id = 1,
                    parentId = -1, 
                    localSupers = new List<long> { 5, 6, 7 } 
                },
                new CategoryView() 
                { 
                    id = 2,
                    parentId = 1, 
                    localSupers = new List<long> { 8, 9 } 
                },
                new CategoryView() 
                { 
                    id = 3,
                    parentId = -1, 
                    localSupers = new List<long> { 1, 2 } 
                },
                new CategoryView() 
                { 
                    id = 4,
                    parentId = 2, 
                    localSupers = new List<long> { 8, 10 } 
                }
            });

            AssertSupersEqual(new[] {5L,6,7}, supers[1]);
            AssertSupersEqual(new[] {5L, 6, 7, 8L,9}, supers[2]); //2 should inherit from 1
            AssertSupersEqual(new[] {1L,2}, supers[3]);
            AssertSupersEqual(new[] {5L, 6, 7, 8L,9,10}, supers[4]); //2 should inherit from 1
        }
    }
}