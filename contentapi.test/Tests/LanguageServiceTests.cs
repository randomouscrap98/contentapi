
using System.Collections.Generic;
using System.IO;
using contentapi.Configs;
using contentapi.Services;
using Xunit;

namespace contentapi.test
{
    public class LanguageServiceTests
    {
        public const string LanguageFolder = "languageTest";

        private LanguageService CreateService()
        {
            var config = new LanguageConfig()
            {
                LanguageFolder = LanguageFolder
            };
            return new LanguageService(config);
        }

        private void CreateLanguageTag(string tag, string language, string content)
        {
            if(!Directory.Exists(LanguageFolder))
                Directory.CreateDirectory(LanguageFolder);

            //WE know how the language service works
            var path = Path.Combine(LanguageFolder, $"{tag}_{language}");

            File.WriteAllText(path, content);
        }

        [Fact]
        public void TestSimpleTag()
        {
            var tag = "test";
            var language = "en";
            var contents = "This is a simple test";
            var service = CreateService();
            CreateLanguageTag(tag, language, contents);
            Assert.Equal(service.GetString(tag, language), contents);
        }

        [Fact]
        public void TestSimpleTagReplace()
        {
            var tag = "test2";
            var language = "en";
            var contents = "This is a {what} test";
            var service = CreateService();
            CreateLanguageTag(tag, language, contents);
            Assert.Equal(service.GetString(tag, language), contents);
            Assert.Equal("This is a crappy test", service.GetString(tag, language, new Dictionary<string, object>(){{"what", "crappy"}}));
            Assert.Equal("This is a very good test", service.GetString(tag, language, new Dictionary<string, object>(){{"what", "very good"}}));
        }
    }
}