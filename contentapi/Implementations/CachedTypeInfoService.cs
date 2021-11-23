using System.Reflection;

namespace contentapi.Implementations;

public class CachedTypeInfoService : ITypeInfoService
{
    protected readonly object cacheLock = new Object();
    protected Dictionary<Type, TypeInfo> cachedTypes = new Dictionary<Type, TypeInfo>();
    protected ILogger logger;

    public CachedTypeInfoService(ILogger<CachedTypeInfoService> logger)
    {
        this.logger = logger;
    }

    public TypeInfo GetTypeInfo<T>()
    {
        var t = typeof(T);
        lock(cacheLock)
        {
            if(!cachedTypes.ContainsKey(t))
            {
                var compattr = typeof(ComputedAttribute);
                var searchattr = typeof(SearchableAttribute);
                var ffattr = typeof(FromFieldAttribute);
                var result = new TypeInfo() { type = t };
                var props = t.GetProperties().Where(x => !Attribute.IsDefined(x, compattr));
                result.queryableFields = props.Select(x => x.Name).ToList();
                result.searchableFields = props.Where(x => Attribute.IsDefined(x, searchattr)).Select(x => x.Name).ToList();
                result.fieldTypes = props.ToDictionary(k => k.Name, v => v.PropertyType);
                result.fieldRemap = props.Where(x => Attribute.IsDefined(x, ffattr)).ToDictionary(k => k.Name, v => v.GetCustomAttribute<FromFieldAttribute>()?.Field ?? 
                    throw new InvalidOperationException("NO FROMFIELD ATTRIBUTE FOUND ON ATTRIBUTE THAT SAID IT HAD ONE"));
                logger.LogInformation($"Added type {t.Name} to cached type service");
                cachedTypes.Add(t, result);
            }
        }
        return cachedTypes[t];
    }
}