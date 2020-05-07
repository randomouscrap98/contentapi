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