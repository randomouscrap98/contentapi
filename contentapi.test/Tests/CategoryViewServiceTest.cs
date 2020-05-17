using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    //This will eventually change to the permission service test. Although content will have something different
    //I suppose... or perhaps just more tests :)
    public class CategoryViewServiceTest : PermissionServiceTestBase<CategoryViewService, CategoryView, CategorySearch>
    {
        public CategoryViewServiceTest() : base() { }

        //Get the boring stuff out of the way.
        [Fact] public override void SimpleEmptyCanUser() { base.SimpleEmptyCanUser(); }
        [Fact] public override void SimpleEmptyRead() { base.SimpleEmptyRead(); }
        [Fact] public override void SimpleOwnerInsert() { base.SimpleOwnerInsert(); }
        [Fact] public override void SimpleOwnerMultiInsert() { base.SimpleOwnerMultiInsert(); }
        [Fact] public override void SimpleOwnerUpdate() { base.SimpleOwnerUpdate(); }
        [Fact] public override void SimpleOwnerDelete() 
        { 
            //You HAVE TO be super user to delete, regardless of if you're the owner or not
            config.SuperUsers.Add(1);
            base.SimpleOwnerDelete(); 
        }
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
        [InlineData("c", 0, "c", false, true)] //Categories are set up so only supers can do it.
        [InlineData("r", 0, "r", false, true)]
        [InlineData("u", 0, "u", false, true)]
        [InlineData("d", 0, "d", false, false)]
        [InlineData("c", 2, "c", false, true)]  //And then for the particular user.
        [InlineData("r", 2, "r", false, true)]
        [InlineData("u", 2, "u", false, true)]
        [InlineData("d", 2, "d", false, false)]
        public override void PermissionGeneral(string action, long permUser, string permValue, bool super, bool allowed)
        {
            base.PermissionGeneral(action, permUser, permValue, super, allowed);
        }
    }
}