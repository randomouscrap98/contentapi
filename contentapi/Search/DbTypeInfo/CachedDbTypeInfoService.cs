using System.Reflection;

namespace contentapi.Search;

public class CacheDbTypeInfoService : IDbTypeInfoService
{
    protected readonly object cacheLock = new Object();
    protected Dictionary<Type, DbTypeInfo> cachedTypes = new Dictionary<Type, DbTypeInfo>();
    protected ILogger logger;

    public CacheDbTypeInfoService(ILogger<CacheDbTypeInfoService> logger)
    {
        this.logger = logger;
    }

    public DbTypeInfo GetTypeInfo<T>()
    {
        return GetTypeInfo(typeof(T));
    }

    public DbTypeInfo GetTypeInfo(Type t)
    {
        lock(cacheLock)
        {
            if(!cachedTypes.ContainsKey(t))
            {
                var compattr = typeof(ComputedAttribute);
                var searchattr = typeof(SearchableAttribute);
                var ffattr = typeof(FromFieldAttribute);
                var exattr = typeof(ExpensiveAttribute);
                var mlattr = typeof(MultilineAttribute);

                var result = new Search.DbTypeInfo() { 
                    type = t,
                    modelType = t.GetCustomAttribute<FromTableAttribute>()?.Type,
                    requestType = t.GetCustomAttribute<ForRequestAttribute>()?.Type
                };

                result.modelTable = (result.modelType ?? t).GetCustomAttribute<Dapper.Contrib.Extensions.TableAttribute>()?.Name;
                result.modelProperties = result.modelType != null ? result.modelType.GetProperties().ToDictionary(x => x.Name, x => x) : new Dictionary<string, PropertyInfo>();

                var allProperties = t.GetProperties().ToDictionary(x => x.Name, x => x);

                foreach(var pk in allProperties)
                {
                    var computed = Attribute.IsDefined(pk.Value, compattr);
                    var writeRule = pk.Value.GetCustomAttribute<WriteRuleAttribute>(); 

                    var fieldInfo = new DbFieldInfo()
                    {
                        rawProperty = pk.Value,
                        queryable = Attribute.IsDefined(pk.Value, searchattr),
                        multiline = Attribute.IsDefined(pk.Value, mlattr),
                        onInsert = writeRule?.InsertRule ?? WriteRuleType.None, 
                        onUpdate = writeRule?.UpdateRule ?? WriteRuleType.None, 
                        computed = computed,
                        realDbColumn = Attribute.IsDefined(pk.Value, ffattr) ? pk.Value.GetCustomAttribute<FromFieldAttribute>()?.Field : computed ? null : pk.Key, //The real db column IS the field name simply by default, unless it's computed
                        expensive = pk.Value.GetCustomAttribute<ExpensiveAttribute>()?.PotentialCost ?? 0
                    };

                    result.fields.Add(pk.Key, fieldInfo);
                }

                logger.LogInformation($"Added type {t.Name} to cached type service");
                cachedTypes.Add(t, result);
            }
        }
        return cachedTypes[t];
    }
}