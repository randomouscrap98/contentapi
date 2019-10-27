using System;
using contentapi.test;
using contentapi;
using contentapi.Controllers;
using contentapi.Models;
using System.Diagnostics;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.performance
{
    class Program
    {
        public const int MinCategories = 6;
        public const int MinContent = 20;
        public const int MinSubcontent = 20;

        static void Main(string[] args)
        {
            var baseTest = new TestBase();
            var controllerTester = new ControllerTestBase<CategoriesController>(baseTest);
            var instance = controllerTester.GetInstance(true);
            //var context = instance.Context;
            var eService = instance.EntityService;

            var stopwatch = new Stopwatch();

            Console.Write("Categories: ");
            int AddCategories = int.Parse(Console.ReadLine());
            Console.Write("Content: ");
            int AddContent = int.Parse(Console.ReadLine());
            Console.Write("Subcontent: ");
            int AddSubcontent = int.Parse(Console.ReadLine());

            if(AddCategories < MinCategories)
                throw new InvalidOperationException($"Must have at least {MinCategories} categories");
            if(AddCategories * AddContent < MinContent)
                throw new InvalidOperationException($"Must have at least {MinContent} content");
            if(AddCategories * AddContent * AddSubcontent < MinSubcontent)
                throw new InvalidOperationException($"Must have at least {MinSubcontent} subcontent");

            Console.WriteLine("Filling the database with crap");
            stopwatch.Start();

            for(int i = 0; i < AddCategories; i++)
            {
                var category = new CategoryEntity()
                {
                    name = $"c_{i}",
                };

                eService.SetNewEntity(category, EntityAction.Read);
                instance.Context.CategoryEntities.Add(category);
                instance.Context.SaveChanges();

                for(int j = 0; j < AddContent; j++)
                {
                    var content = new ContentEntity()
                    {
                        title = $"ct_{j}",
                        content = $"{j}/{i}",
                        categoryId = category.entityId
                    };

                    eService.SetNewEntity(content, EntityAction.Read);
                    instance.Context.ContentEntities.Add(content);
                    instance.Context.SaveChanges();

                    for(int k = 0; k < AddSubcontent; k++)
                    {
                        var sub = new SubcontentEntity()
                        {
                            content = $"{k}/{j}/{i}",
                            contentId = content.entityId
                        };

                        eService.SetNewEntity(sub, EntityAction.Read);
                        instance.Context.SubcontentEntities.Add(sub);
                    }

                    instance.Context.SaveChanges();
                    Console.WriteLine($"{(i * AddContent + j)/(double)(AddContent * AddCategories)*100:0.##}%"); 

                    //RECREATE the dbcontext (instance) after every big subcontent insert. this keeps speed up
                    instance = controllerTester.GetInstance(true);
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"Adding items took {stopwatch.Elapsed.TotalSeconds:0.##} seconds");

            var subcontentTester = new ControllerTestBase<SubcontentController>(baseTest);
            var subIntance = subcontentTester.GetInstance(false);

            BasicTest<SubcontentController, SubcontentEntity, SubcontentView>(
                Math.Max(MinSubcontent / 2, AddSubcontent / 10), AddCategories * AddContent * AddSubcontent, baseTest);

            BasicTest<ContentController, ContentEntity, ContentView>(
                Math.Max(MinContent / 2, AddContent / 10), AddCategories * AddContent, baseTest);

            BasicTest<CategoriesController, CategoryEntity, CategoryView>(
                Math.Max(MinCategories / 2, AddCategories / 2), AddCategories, baseTest);

            Console.WriteLine("All done!");
            Console.ReadKey(true);
        }

        public static T TimeThing<T>(Func<ActionResult<T>> thing, TestBase baseTest)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = thing();
            Assert.True(baseTest.IsSuccessRequest(result));
            stopwatch.Stop();
            Console.WriteLine($"Took: {stopwatch.Elapsed.TotalMilliseconds:0.##} milliseconds");
            return result.Value;
        }

        public static void BasicTest<T,U,V>(int count, int total, TestBase baseTest) where T : EntityController<U,V> where U : EntityChild where V : EntityView
        {
            var subcontentTester = new ControllerTestBase<T>(baseTest);
            var subIntance = subcontentTester.GetInstance(false);
            var name = typeof(U).Name;

            Console.WriteLine($"Requesting {count} {name} out of {total}");
            var result = TimeThing(() => subIntance.Controller.Get(new Services.CollectionQuery() { count = count } ).Result, baseTest);
            var items = subIntance.Controller.GetCollectionFromResult<V>(result).ToList();

            Console.WriteLine($"Requesting next {count} {name} out of {total}");
            var result2 = TimeThing(() => subIntance.Controller.Get(new Services.CollectionQuery() { offset = count, count = count } ).Result, baseTest);
            var items2 = subIntance.Controller.GetCollectionFromResult<V>(result2).ToList();

            Console.WriteLine($"Requesting some random {name} out of {total}");
            TimeThing(() => subIntance.Controller.GetSingle(items.First().id).Result, baseTest);

            Console.WriteLine($"Requesting some other random {name} out of {total}");
            TimeThing(() => subIntance.Controller.GetSingle(items2.Last().id).Result, baseTest);
        }
    }
}
