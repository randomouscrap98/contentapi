using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using contentapi.Services.Views.Implementations;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;
using Xunit;

namespace contentapi.test
{
    public class PermissionServiceTestBase<T, V, S> : ServiceConfigTestBase<T, SystemConfig> where T : BasePermissionViewService<V, S> where V : StandardView, new() where S : BaseSearch, new()
    {
        protected SystemConfig sysConfig = new SystemConfig();

        protected override SystemConfig config => sysConfig;

        //Assume most things can have a parent that is of the same type (this is mostly true...)
        public virtual long SetupParent(Action<BasePermissionView> modify = null) //BAD DEPENDENCIES TEST BREAK AGGHH
        {
            var view = new V() { };
            if(modify != null)
                modify(view);
            //view.permissions.Add("0", defaultPerms);
            view = service.WriteAsync(view, new Requester(){system = true}).Result; //This will result in a creator of 0
            return view.id;
        }

        public virtual async Task<V> BasicInsertAsync(Requester requester, Action<V> modify = null, bool insertThrows = false, Action<BasePermissionView> parentModify = null)//string parentDefaultPerms = "C")
        {
            //Assume you want to be able to create in the parent lol
            if(parentModify == null)
                parentModify = (v) => v.permissions.Add("0", "C");

            var parentId = SetupParent(parentModify);
            var view = new V() { parentId = parentId };

            if(modify != null) 
                modify(view);

            try
            {
                var writeView = await service.WriteAsync(view, requester);
                return writeView;
            }
            catch(AuthorizationException)
            {
                if(!insertThrows)
                    throw;

                return view;
            }
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
            Assert.Equal(expected.permissions, check.permissions);
        }

        public virtual void SimpleEmptyCanUser()
        {
            var result = service.CanUser(new Requester(){userId = 1}, Keys.UpdateAction, NewPackage());
            Assert.False(result);
        }

        //Yes, it is important to read nothing.
        public virtual void SimpleEmptyRead()
        {
            var result = service.SearchAsync(new S(), new Requester() { userId = 1}).Result;
            Assert.Empty(result);
        } 

        //Now insert a single thing and make sure we can read it. Also make sure various fields are OK.
        //This is also the test that ensures the data is written relatively properly.
        public virtual void SimpleOwnerInsert() { SimpleOwnerInsertId(1); }
        public virtual void SimpleOwnerInsertId(long userId)
        {
            var start = DateTime.Now;
            var requester = new Requester() {userId = userId};
            var writeView = BasicInsertAsync(requester).Result;

            var readView = FindByIdAsync(writeView.id, requester).Result;
            Assert.NotNull(readView);

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
                SimpleOwnerInsertId(i);
                search.Ids.Add(i);
                var result = service.SearchAsync(search, new Requester() {system = true}).Result;  //Get them ALL
                Assert.Equal(i, result.Count);
            }
        }

        //Test ONLY if the owner CAN update their own view (they should be able to)
        public virtual void SimpleOwnerUpdate() { SimpleOwnerUpdateId(1); }
        public virtual void SimpleOwnerUpdateId(long userId)
        {
            //var view = NewView();
            var start = DateTime.Now;
            var requester = new Requester() { userId = userId};

            var writeView = BasicInsertAsync(requester).Result; //service.WriteAsync(view, requester).Result;

            //Owners should be able to SPECIFICALLY modify permissions
            writeView.permissions.Add("0", "CR");
            var writeView2 = service.WriteAsync(writeView, requester).Result;

            Assert.NotEqual(writeView2.permissions, writeView.permissions);

            var readViews = service.SearchAsync(new S(), requester).Result; //owners should always be able to read this
            Assert.Single(readViews); //Just to make sure an update didn't create a copy.

            var readView = readViews.First();

            //Oh right: updating also ensures the fields are set properly. This won't have to be tested in other updates.
            Assert.Equal(userId, readView.createUserId);
            Assert.Equal(userId, readView.editUserId);
            Assert.True(readView.createDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.True(readView.editDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.NotEqual(readView.editDate, readView.createDate);
            Assert.True(readView.id > 0);
            Assert.True(writeView2.permissions.ContainsKey("0"));
            Assert.True(writeView2.permissions["0"].ToLower() == "cr" || writeView2.permissions["0"].ToLower() == "rc");

            //I don't assume what edit date/user will be on create.
            AssertViewsEqual(readView, writeView2);
        }


        public virtual void SimpleOwnerDelete() { SimpleOwnerDeleteId(1); }
        public virtual void SimpleOwnerDeleteId(long userId)
        {
            //var view = NewView();
            var start = DateTime.Now;
            var requester = new Requester() { userId = userId};

            var writeView = BasicInsertAsync(requester).Result; //service.WriteAsync(view, requester).Result;
            var readViews = service.SearchAsync(new S(), requester).Result; //owners should always be able to read this
            Assert.Single(readViews); //Assume this is us
            Assert.Equal(readViews.First().id, writeView.id);

            var deleteView = service.DeleteAsync(writeView.id, requester).Result;

            Assert.True(deleteView.createDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.True(deleteView.editDate - start < TimeSpan.FromSeconds(60)); //Make sure the date is KINDA close
            Assert.True(deleteView.id > 0);

            //I don't assume what edit date/user will be on create.
            AssertViewsEqual(readViews.First(), deleteView);

            readViews = service.SearchAsync(new S(), requester).Result; //owners should always be able to read this
            Assert.Empty(readViews);
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

        public virtual async Task<Requester> CreateFakeUserAsync()
        {
            var provider = CreateService<IEntityProvider>();
            var entity = new Entity() { type = Keys.UserType};
            await provider.WriteAsync(entity);

            return new Requester { userId = entity.id };
        }

        public virtual async Task<V> FindByIdAsync(long id, Requester requester)
        {
            var search = new S();
            search.Ids.Add(id);
            return (await service.SearchAsync(search, requester)).OnlySingle();
        }

        //PRETTY DANG HACKY IF I DO SAY SO MYSELF. This assumes owners can always do everything on their 
        //own content, so I'm not even bothering testing that. All tests are on content someone else owns.
        public virtual void PermissionGeneral(string action, long permUser, string permValue, bool super, bool allowed)
        {
            if(super)
                config.SuperUsers.Add(2);
            else
                config.SuperUsers.RemoveAll(x => x == 2);

            //Insert two "users", these will be our requesters.
            var isAction = new Func<string, bool>((s) => action.ToLower().StartsWith(s.ToLower()));

            var creator = CreateFakeUserAsync().Result;
            var requester = CreateFakeUserAsync().Result;

            if(isAction("c"))
            {
                //This will throw on fail, so...
                var view = BasicInsertAsync(requester, null, !allowed, (v) => v.permissions.Add(permUser.ToString(), permValue)).Result;
            }
            else
            {
                var view = BasicInsertAsync(creator, (v) => v.permissions.Add(permUser.ToString(), permValue)).Result;
                //Don't set anything on the view for update, will this be an acceptable test?
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

                if(isAction("r"))
                {
                    var result = FindByIdAsync(view.id, requester).Result; //service.SearchAsync(search, requester).Result;

                    if(allowed)
                        Assert.NotNull(result);
                    else
                        Assert.Null(result);
                }
                else if(isAction("u"))
                {
                    tryAction(() => service.WriteAsync(view, requester).Wait());
                }
                else if(isAction("d"))
                {
                    tryAction(() => service.DeleteAsync(view.id, requester).Wait());
                }
                else
                {
                    throw new InvalidOperationException($"Unknown action type: {action}");
                }
            }
        }

        //public virtual void SimpleRegularCreate()
        //{
        //    //We should NOT be able to insert into a parent without create. We SHOULD be able to otherwise.
        //}
    }
}