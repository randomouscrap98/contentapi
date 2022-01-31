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
                var autodattr = typeof(AutoDateAttribute);
                var autouattr = typeof(AutoUserIdAttribute);
                var pouattr = typeof(PreserveOnUpdateAttribute);

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

                    var fieldInfo = new DbFieldInfo()
                    {
                        rawProperty = pk.Value,
                        //matchedModelProperty = dbModelProperties.ContainsKey(pk.Key) ? dbModelProperties[pk.Key] : null,
                        queryable = Attribute.IsDefined(pk.Value, searchattr),
                        preserveOnUpdate = Attribute.IsDefined(pk.Value, pouattr),
                        computed = computed,
                        realDbColumn = Attribute.IsDefined(pk.Value, ffattr) ? pk.Value.GetCustomAttribute<FromFieldAttribute>()?.Field : computed ? null : pk.Key, //The real db column IS the field name simply by default, unless it's computed
                        expensive = Attribute.IsDefined(pk.Value, exattr) ? pk.Value.GetCustomAttribute<ExpensiveAttribute>()?.PotentialCost 
                            ?? throw new InvalidOperationException("NO EXPENSIVE ATTRIBUTE FOUND ON ATTRIBUTE THAT SAID IT HAD ONE") : -1 
                    };

                    if(Attribute.IsDefined(pk.Value, autodattr))
                    {
                        var autodate = pk.Value.GetCustomAttribute<AutoDateAttribute>() ?? throw new InvalidOperationException("NO AUTODATE ATTRIBUTE FOUND ON ATTRIBUTE THAT SAID IT HAD IT");
                        fieldInfo.autoDateOnInsert = autodate.OnInsert;
                        fieldInfo.autoDateOnUpdate = autodate.OnUpdate;
                    }

                    if(Attribute.IsDefined(pk.Value, autouattr))
                    {
                        var autouser = pk.Value.GetCustomAttribute<AutoUserIdAttribute>() ?? throw new InvalidOperationException("NO AUTOUSER ATTRIBUTE FOUND ON ATTRIBUTE THAT SAID IT HAD IT");
                        fieldInfo.autoDateOnInsert = autouser.OnInsert;
                        fieldInfo.autoDateOnUpdate = autouser.OnUpdate;
                    }

                    result.fields.Add(pk.Key, fieldInfo);
                }

                //var props = result.properties.Values.Where(x => !Attribute.IsDefined(x, compattr));

                //All of these could be null, and that's ok! If it's a complex type,
                //we kind of expect it to be null


                //result.queryableFields = props.Select(x => x.Name).ToList();
                //result.searchableFields = props.Where(x => Attribute.IsDefined(x, searchattr)).Select(x => x.Name).ToList();
                //result.fieldTypes = props.ToDictionary(k => k.Name, v => v.PropertyType);
                //result.fieldRemap = props.Where(x => Attribute.IsDefined(x, ffattr)).ToDictionary(k => k.Name, v => v.GetCustomAttribute<FromFieldAttribute>()?.Field ?? 
                //    throw new InvalidOperationException("NO FROMFIELD ATTRIBUTE FOUND ON ATTRIBUTE THAT SAID IT HAD ONE"));

                logger.LogInformation($"Added type {t.Name} to cached type service");
                cachedTypes.Add(t, result);
            }
        }
        return cachedTypes[t];
    }
}