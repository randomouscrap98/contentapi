using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace contentapi.Services.Implementations
{
    public class LanguageConfig
    {
        public string LanguageFolder {get;set;} = null;
        public string DefaultLanguage {get;set;} = "en";
    }

    public class LanguageService : ILanguageService
    {
        //Absolutely NO caching right now!
        public LanguageConfig Config;

        public LanguageService(LanguageConfig config)
        {
            this.Config = config;
        }

        public string GetTagReplaceable(string tag)
        {
            return "{" + tag + "}";
        }

        public string GetString(string tag, string language, Dictionary<string, object> values = null)
        {
            values = values ?? new Dictionary<string, object>();

            //Construct the filename
            var filename = $"{tag}_{language}";
            var filePath = Path.Combine(Config.LanguageFolder, filename);

            //Rerun with the default language to see if that's THERE, otherwise BLEGH QUIT
            if(!File.Exists(filePath))
            {
                if(language != Config.DefaultLanguage)
                    return GetString(tag, Config.DefaultLanguage, values);
                else
                    throw new InvalidOperationException("Could not find tag specified for given language!");
            }

            var builder = new StringBuilder(File.ReadAllText(filePath));

            //Replace tags (whenever found) in the base string. At this time, DON'T worry about 
            //tags that aren't used... we'll worry about all that later!
            foreach(var keyValue in values)
                builder.Replace(GetTagReplaceable(keyValue.Key), keyValue.Value.ToString());

            return builder.ToString();
        }

        public string GetString(string tag, Language language, Dictionary<string, object> values = null)
        {
            return GetString(tag, language.ToString("G").ToLower(), values);
        }
    }
}