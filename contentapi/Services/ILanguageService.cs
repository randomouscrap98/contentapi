using System.Collections.Generic;

namespace contentapi.Services
{
    public enum Language
    {
        en, fr, es, ja
    }

    public interface ILanguageService
    {
        string GetString(string tag, string language, Dictionary<string, object> values = null);
        string GetString(string tag, Language language, Dictionary<string, object> values = null);
    }

}