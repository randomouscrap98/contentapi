using System.Reflection;
using contentapi.Search;

namespace contentapi.Utilities;

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
        return GetTypeInfo(typeof(T));
    }

    public TypeInfo GetTypeInfo(Type t)
    {
        lock(cacheLock)
        {
            if(!cachedTypes.ContainsKey(t))
            {
                var result = new TypeInfo() { 
                    type = t,
                    properties = t.GetProperties().ToDictionary(x => x.Name, x => x)
                };

                var compattr = typeof(ComputedAttribute);
                var searchattr = typeof(SearchableAttribute);
                var ffattr = typeof(FromFieldAttribute);
                var props = result.properties.Values.Where(x => !Attribute.IsDefined(x, compattr));

                //All of these could be null, and that's ok! If it's a complex type,
                //we kind of expect it to be null
                result.tableType = t.GetCustomAttribute<FromTableAttribute>()?.Type;
                result.table = (result.tableType ?? t).GetCustomAttribute<Dapper.Contrib.Extensions.TableAttribute>()?.Name;
                result.requestType = t.GetCustomAttribute<ForRequestAttribute>()?.Type;

                //Go get the table type properties, just to make life eaiser. The table type should be a db model... generally
                if(result.tableType != null)
                    result.tableTypeProperties = result.tableType.GetProperties().ToDictionary(x => x.Name, x => x);

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