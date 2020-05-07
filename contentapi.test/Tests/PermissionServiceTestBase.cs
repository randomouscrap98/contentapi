using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;
using Xunit;

namespace contentapi.test
{
    public class PermissionServiceTestBase<T, V, S> : ServiceConfigTestBase<T, SystemConfig> where T : BasePermissionViewService<V, S> where V : BasePermissionView, new() where S : EntitySearchBase, new()
    {
        protected SystemConfig sysConfig = new SystemConfig();

        protected override SystemConfig config => sysConfig;

        public long parentId = -1;

        //Assume most things can have a parent that is of the same type (this is mostly true...)
        public virtual long SetupParent()
        {
            var view = new V() { };
            view.permissions.Add("0", "C"); //BAD DEPENDENCIES TEST BREAKK AGHHG
            view = service.WriteAsync(view, new Requester(){system = true}).Result; //This will result in a creator of 0
            return view.id;
        }

        public virtual V NewView()
        {
            if(parentId <= 0)
                parentId = SetupParent();

            return new V() { parentId = parentId };
        }

        public void AssertViewsEqual(V expected, V check)
        {
            //Things I care about
            Assert.Equal(expected.id, check.id);
            Assert.Equal(expected.createDate, check.createDate);
            Assert.Equal(expected.editDate, check.editDate);
            Assert.Equal(expected.createUserId, check.createUserId);
            Assert.Equal(expected.editUserId, check.editUserId);
            Assert.Equal(expected.parentId, check.parentId);
        }

        public virtual void SimpleEmptyCanUser()
        {
            var result = service.CanUser(new Requester(){userId = 1}, keys.UpdateAction, NewPackage());
            Assert.False(result);
        }

        //Yes, it is important to read nothing.
        public virtual void SimpleEmptyRead()
        {
            var result = service.SearchAsync(new S(), new Requester() { userId = 1}).Result;
            Assert.Empty(result);
        } 

        public virtual void SimpleEmptyTests()
        {
            SimpleEmptyCanUser();
            SimpleEmptyRead();
        }

        //Now insert a single thing and make sure we can read it. Also make sure various fields are OK
        public virtual void SimpleOwnerInsert(long userId = 1)
        {
            var view = NewView();
            var start = DateTime.Now;
            var requester = new Requester() { userId = userId };

            var writeView = service.WriteAsync(view, requester).Result;

            var search = new S();
            search.Ids.Add(writeView.id);

            var readViews = service.SearchAsync(search, requester).Result; //owners should always be able to read this

            Assert.Single(readViews);

            var readView = readViews.First();

            Assert.Equal(userId, readView.createUserId);
            Assert.True(readView.createDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.True(readView.id > 0);

            AssertViewsEqual(readView, writeView);

            //I don't assume what edit date/user will be on create.
        }

        public virtual void SimpleOwnerMultiInsert()
        {
            var search = new S();
            for(var i = 1; i <= 10; i++)
            {
                SimpleOwnerInsert(i);
                search.Ids.Add(i);
                var result = service.SearchAsync(search, new Requester() {system = true}).Result;  //Get them ALL
                Assert.Equal(i, result.Count);
            }
        }

        public virtual void SimpleOwnerUpdate()
        {
            var view = NewView();
            var start = DateTime.Now;
            var requester = new Requester() { userId = 1};

            var writeView = service.WriteAsync(view, requester).Result;
            var readViews = service.SearchAsync(new S(), requester).Result; //owners should always be able to read this

            Assert.Single(readViews);

            var readView = readViews.First();

            Assert.Equal(1, readView.createUserId);
            Assert.True(readView.createDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.True(readView.id > 0);

            //I don't assume what edit date/user will be on create.
        }

        public virtual void SimpleOwnerTests()
        {
            SimpleOwnerInsert();
            SimpleOwnerMultiInsert();
        }

        //For now, you CANNOT insert things without a parent.
        public virtual void SimpleNoParentSuper()
        {
            var requester = new Requester() { userId = 1 };

            for(var i = 0; i < 10; i++)
            {
                sysConfig.SuperUsers.Clear();
                //make sure you can alternate with these (just to be sure)
                var view = new V(); //This way does not make a parent

                AssertThrows<Exception>(() => { var w = service.WriteAsync(view, requester).Result; });
                sysConfig.SuperUsers.Add(1);
                var writeView = service.WriteAsync(view, requester).Result;
                Assert.True(writeView.id > 0);
            }
        }

        public virtual void SimpleSuperTests()
        {
            SimpleNoParentSuper();
        }
    }
}