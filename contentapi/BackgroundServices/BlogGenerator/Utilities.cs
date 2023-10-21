using contentapi.data;
using Newtonsoft.Json;

namespace blog_generator;

public static class Utilities
{
    public static T ForceCastResult<T>(object item)
    {
        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(item)) ??
            throw new InvalidOperationException($"Can't cast item to {typeof(T)}");
    }

    public static List<T> ForceCastResultObjects<T>(GenericSearchResult result, string key, string onBehalf)
    {
        return ForceCastResultObjects<T>(result.objects, key, onBehalf);
    }

    public static List<T> ForceCastResultObjects<T>(Dictionary<string, IEnumerable<IDictionary<string, object>>> objects, string key, string onBehalf)
    {
        if(!objects.ContainsKey(key))
            throw new InvalidOperationException($"No {key} result in {onBehalf}!!");

        return objects[key].Select(x => ForceCastResult<T>(x)).ToList();
    }

}