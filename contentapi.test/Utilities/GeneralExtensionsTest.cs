using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using contentapi.Utilities;
using Xunit;

namespace contentapi.test;

public class GeneralExtensionsTest : UnitTestBase
{
    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(long), false)]
    [InlineData(typeof(GeneralExtensions), false)]
    [InlineData(typeof(IEnumerable<long>), true)]
    [InlineData(typeof(List<long>), true)]
    [InlineData(typeof(IEnumerable<string>), true)]
    [InlineData(typeof(List<string>), true)]
    [InlineData(typeof(IEnumerable<IEnumerable<Type>>), true)]
    [InlineData(typeof(List<IEnumerable<Type>>), true)]
    [InlineData(typeof(IDictionary<long, string>), false)]
    [InlineData(typeof(Dictionary<long, string>), false)]
    [InlineData(typeof(IDictionary<string, IEnumerable<Type>>), false)]
    [InlineData(typeof(Dictionary<string, IEnumerable<Type>>), false)]
    public void IsGenericEnumerable(Type type, bool expected)
    {
        var message = $"{type} supposed to be IEnumerable: {expected}";

        if(expected)
            Assert.True(type.IsGenericEnumerable(), message);
        else
            Assert.False(type.IsGenericEnumerable(), message);
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(long), false)]
    [InlineData(typeof(GeneralExtensions), false)]
    [InlineData(typeof(IDictionary<long, string>), true)]
    [InlineData(typeof(Dictionary<long, string>), true)]
    [InlineData(typeof(IDictionary<string, object>), true)]
    [InlineData(typeof(Dictionary<string, object>), true)]
    [InlineData(typeof(IDictionary<string, IDictionary<string, Type>>), true)]
    [InlineData(typeof(Dictionary<string, IDictionary<string, Type>>), true)]
    public void IsGenericDictionary(Type type, bool expected)
    {
        var message = $"{type} supposed to be IDictionary: {expected}";

        if(expected)
            Assert.True(type.IsGenericDictionary(), message);
        else
            Assert.False(type.IsGenericDictionary(), message);
    }

    [Fact]
    public async Task TemporaryFileTask_AllTest()
    {
        var folder = GetTempFolder(false);
        var filecontents = "Hello, this is a file\nAnd stuff is happening with it!!";
        using(var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(filecontents)))
        {
            var generatedPath = "";
            await stream.TemporaryFileTask(folder, async (path) =>
            {
                generatedPath = path;
                Assert.True(File.Exists(generatedPath));
                Assert.Equal(filecontents, await File.ReadAllTextAsync(path));
            });
            Assert.False(File.Exists(generatedPath));
        }
    }
}