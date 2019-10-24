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
    }
}