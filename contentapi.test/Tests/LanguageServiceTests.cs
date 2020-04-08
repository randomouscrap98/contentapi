using System.Collections.Generic;
using System.IO;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace contentapi.test
{
    public class LanguageServiceTests : ServiceConfigTestBase<LanguageService, LanguageConfig>
    {
        public const string LanguageFolder = "languageTest";

        protected override LanguageConfig config { get => new LanguageConfig() { LanguageFolder = LanguageFolder };}

        //public LanguageServiceTests() { config.LanguageFolder = LanguageFolder; }

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
            CreateLanguageTag(tag, language, contents);
            Assert.Equal(service.GetString(tag, language), contents);
        }

        [Fact]
        public void TestSimpleTagReplace()
        {
            var tag = "test2";
            var language = "en";
            var contents = "This is a {what} test";
            CreateLanguageTag(tag, language, contents);
            Assert.Equal(contents, service.GetString(tag, language));
            Assert.Equal("This is a crappy test", service.GetString(tag, language, new Dictionary<string, object>(){{"what", "crappy"}}));
            Assert.Equal("This is a very good test", service.GetString(tag, language, new Dictionary<string, object>(){{"what", "very good"}}));
        }

        [Fact]
        public void TestDefaultLanguageFallback()
        {
            var tag = "test3";
            var contents = "Some serious {thing}";
            CreateLanguageTag(tag, "en", contents);
            Assert.Equal(contents, service.GetString(tag, "fr")); //this should fallback to default
            Assert.Equal("Some serious donkeys", service.GetString(tag, "fr", new Dictionary<string, object>() {{"thing", "donkeys"}})); //this should fallback to default
        }
    }
}