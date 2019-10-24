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

        [Fact]
        public void TestBasicCreate()
        {
            var view = CreateView();
            var addedView = CreateEntity(view);
            Assert.Equal(view.name, addedView.name);
            Assert.True(addedView.id > 0);
        }
    }
}