using System.Collections.Generic;
using System.Linq;
using contentapi.Services;
using Newtonsoft.Json;

namespace contentapi.test.Overrides
{
    public class FakeLanguage : ILanguageService
    {
        public string GetString(string tag, string language, Dictionary<string, object> values = null)
        {
            values = values ?? new Dictionary<string, object>();

            if(values.Count == 0)
                return "";
            else if(values.Count == 1)
                return values.First().Value.ToString();
            else
                return JsonConvert.SerializeObject(values);
        }

        public string GetString(string tag, Language language, Dictionary<string, object> values = null)
        {
            return GetString(tag, language.ToString("G"), values);
        }
    }
}