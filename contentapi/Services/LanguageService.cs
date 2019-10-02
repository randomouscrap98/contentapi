using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace contentapi.Services
{
    public interface ILanguageService
    {
        string GetString(string tag, string language, Dictionary<string, object> values = null);
        string GetString(string tag, Language language, Dictionary<string, object> values = null);
    }

    public enum Language
    {
        en, fr, es, ja
    }

    public class LanguageService : ILanguageService
    {
        //Absolutely NO caching right now!
        public string LanguageFolder;
        public string DefaultLanguage = Language.en.ToString("g");

        public LanguageService(string folder)
        {
            LanguageFolder = folder;
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
            var filePath = Path.Combine(LanguageFolder, filename);

            //Rerun with the default language to see if that's THERE, otherwise BLEGH QUIT
            if(!File.Exists(filePath))
            {
                if(language != DefaultLanguage)
                    return GetString(tag, DefaultLanguage, values);
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