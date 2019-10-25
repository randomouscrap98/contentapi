using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;
using contentapi.Services;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using contentapi.test.Overrides;

namespace contentapi.test
{
    public class EntityControllerTest : ControllerTestBase<OpenController>
    {
        private ControllerInstance<OpenController> baseInstance;

        //Every time it starts, clear out the users so each test has a clean slate
        public EntityControllerTest()
        {
            baseInstance = GetInstance(true);
            baseInstance.Controller.ClearAllEntities();
        }

        public CategoryView CreateView(string baseAccess = "CRUD")
        {
            return new CategoryView()
            {
                baseAccess = baseAccess,
                accessList = new Dictionary<string, string>() {},
                name = "c_ " + UniqueSection()
            };
        }

        public CategoryView CreateEntity(CategoryView existing = null, ControllerInstance<OpenController> instance = null)
        {
            existing = existing ?? CreateView();
            instance = instance ?? baseInstance;
            var result = instance.Controller.Post(existing).Result;
            return result.Value;
        }

        public void ConfirmViewSimple(CategoryView view, CategoryView addedView)
        {
            Assert.Equal(view.name, addedView.name);
            Assert.True(addedView.id > 0);
            Assert.True(addedView.createDate > DateTime.Now.AddHours(-1));
            Assert.True(addedView.createDate <= DateTime.Now);
        }

        [Fact]
        public void TestBasicCreate()
        {
            var view = CreateView();
            var addedView = CreateEntity(view);
            ConfirmViewSimple(view, addedView);
        }

        [Fact]
        public void TestCreateIgnoreFields()
        {
            var view = CreateView();
            var fakeDate = new DateTime(2000, 1, 28);
            view.id = 555;
            view.createDate = fakeDate;
            var addedView = CreateEntity(view);
            ConfirmViewSimple(view, addedView);
            Assert.False(addedView.id == view.id);
        }

        [Fact]
        public void TestSimpleRead()
        {
            var view = CreateEntity();
            var result = baseInstance.Controller.GetSingle(view.id).Result;
            Assert.True(IsSuccessRequest(result));
            var retrievedView = result.Value;
            ConfirmViewSimple(view, retrievedView);
            Assert.Equal(view.id, retrievedView.id);
        }

        [Fact]
        public void TestSimpleCantRead()
        {
            var view = CreateView("");
            var addedView = CreateEntity(view); //What happens when you can't read your own view???
            ConfirmViewSimple(view, addedView);
            //OK but now we try to read it again
            var result = baseInstance.Controller.GetSingle(view.id).Result;
            Assert.False(IsSuccessRequest(result)); //Could be unauthorized... could be not found. IDC which
        }

        [Fact]
        public void TestComplexMultiRead()
        {
            var views = new List<CategoryView>();
            views.Add(CreateView());  //This one should be readable.
            views.Add(CreateView(""));
            views.Last().accessList.Add(baseInstance.User.entityId.ToString(), "R"); //This one should also be readable
            views.Add(CreateView("")); //This one is NOT readable
            views.Add(CreateView());
            views.Last().accessList.Add(baseInstance.User.entityId.ToString(), "R"); //This one is MEGA readable.
            //Do this to create another use. VERY BAD
            var anotherContext = GetInstance(true);
            views.Add(CreateView(""));
            views.Last().accessList.Add(anotherContext.User.entityId.ToString(), "R"); //Someone ELSE can read this one.
            views.Add(CreateView("")); //This one is NOT readable (again)

            foreach(var view in views)
            {
                var postResult = baseInstance.Controller.Post(view).Result;
                Assert.True(IsSuccessRequest(postResult));
            }

            var result = baseInstance.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            var allSeeable = ((IEnumerable<CategoryView>)result.Value["collection"]).ToList();
            Assert.Equal(3, allSeeable.Count);
            Assert.Equal(2, allSeeable.Count(x => x.baseAccess.Contains("R")));
            Assert.Equal(2, allSeeable.Count(x => x.accessList.Any(y => y.Key == baseInstance.User.entityId.ToString() && y.Value.Contains("R"))));
            Assert.Equal(0, allSeeable.Count(x => x.accessList.Any(y => y.Key == anotherContext.User.entityId.ToString() && y.Value.Contains("R"))));

            result = anotherContext.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            allSeeable = ((IEnumerable<CategoryView>)result.Value["collection"]).ToList();
            Assert.Equal(3, allSeeable.Count);
            Assert.Equal(2, allSeeable.Count(x => x.baseAccess.Contains("R")));
            Assert.Equal(1, allSeeable.Count(x => x.accessList.Any(y => y.Key == anotherContext.User.entityId.ToString() && y.Value.Contains("R"))));
            Assert.Equal(1, allSeeable.Count(x => x.accessList.Any(y => y.Key == baseInstance.User.entityId.ToString() && y.Value.Contains("R"))));
        }

        [Fact]
        public void TestSimpleUpdate()
        {
            var view = CreateEntity();
            view.name = "SOMETHING ENTIRELY DIFFERENT";
            var result = baseInstance.Controller.Put(view.id, view).Result;
            Assert.True(IsSuccessRequest(result));
            var retrievedView = baseInstance.Controller.GetSingle(view.id).Result.Value;
            ConfirmViewSimple(view, retrievedView);
            Assert.Equal(view.id, retrievedView.id);
        }

        [Fact]
        public void TestSimpleUpdateFail()
        {
            var echView = CreateView("CRD"); //Notice NO update
            var view = CreateEntity(echView);
            view.name = "SOMETHING ENTIRELY DIFFERENT";
            var result = baseInstance.Controller.Put(view.id, view).Result;
            Assert.False(IsSuccessRequest(result)); //Again, don't care if it's unauthorized or not found idk
        }

        [Fact]
        public void TestSimpleDelete()
        {
            var view = CreateEntity();
            var result = baseInstance.Controller.Delete(view.id).Result;
            Assert.True(IsSuccessRequest(result));
            var retrieveResult = baseInstance.Controller.GetSingle(view.id).Result;
            Assert.False(IsSuccessRequest(retrieveResult)); //Again, don't care if it's unauthorized or not found idk
        }

        [Fact]
        public void TestSimpleDeleteFail()
        {
            var echView = CreateView("CRU"); //Notice NO delete
            var view = CreateEntity(echView);
            var result = baseInstance.Controller.Delete(view.id).Result;
            Assert.False(IsSuccessRequest(result)); //Again, don't care if it's unauthorized or not found idk
            var retrievedView = baseInstance.Controller.GetSingle(view.id).Result.Value;
            ConfirmViewSimple(view, retrievedView);
            Assert.Equal(view.id, retrievedView.id);
        }
    }
}