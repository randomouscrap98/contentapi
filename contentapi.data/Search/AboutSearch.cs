using System.Collections.Generic;

namespace contentapi.data;

public class AboutSearch
{
    public Dictionary<string, object> types = new Dictionary<string, object>();
    public Dictionary<string, object> macros = new Dictionary<string, object>();
    public Dictionary<string, object> objects = new Dictionary<string, object>();
    public Dictionary<string, Dictionary<int, string>> codes = new Dictionary<string, Dictionary<int, string>>();
}