using System;
using contentapi.test;
using contentapi;
using contentapi.Controllers;

namespace contentapi.performance
{
    class Program
    {
        static void Main(string[] args)
        {
            var controllerTester = new ControllerTestBase<CategoriesController>();
            Console.WriteLine("Hello World!");
        }
    }
}
