using System.Reflection;
using Dapper.Contrib.Extensions;

namespace contentapi.Search;

public class ViewTypeInfoService_Cached : IViewTypeInfoService
{
    protected readonly object cacheLock = new Object();
    protected Dictionary<Type, ViewTypeInfo> cachedTypes = new Dictionary<Type, ViewTypeInfo>();
    protected ILogger logger;

    public ViewTypeInfoService_Cached(ILogger<ViewTypeInfoService_Cached> logger)
    {
        this.logger = logger;
    }

    public ViewTypeInfo GetTypeInfo<T>()
    {
        return GetTypeInfo(typeof(T));
    }

    public DbTypeInfo GetDbTypeInfo<T>()
    {
        return GetDbTypeInfo(typeof(T));
    }

    public DbTypeInfo GetDbTypeInfo(Type modelType)
    {
        return new DbTypeInfo()
        {
            modelType = modelType,
            modelTable = modelType.GetCustomAttribute<TableAttribute>()?.Name ?? throw new InvalidOperationException($"Db model {modelType} has no associated table??"),
            modelProperties = modelType.GetProperties().ToDictionary(x => x.Name, x => x)
        };
    }

    public ViewTypeInfo GetTypeInfo(Type t)
    {
        lock(cacheLock)
        {
            if(!cachedTypes.ContainsKey(t))
            {
                var result = new Search.ViewTypeInfo() { 
                    type = t,
                    selectFromSql = t.GetCustomAttribute<SelectFromAttribute>()?.SelectFrom_Sql ?? "",
                    whereSql = t.GetCustomAttribute<WhereAttribute>()?.Where_Sql ?? "",
                    groupBySql = t.GetCustomAttribute<GroupByAttribute>()?.GroupBy_Sql ?? "",
                    extraQueryFields = t.GetCustomAttribute<ExtraQueryFieldsAttribute>()?.Fields ?? new List<string>(),
                    requestType = t.GetCustomAttribute<ResultForAttribute>()?.Type
                };

                var writeAs = t.GetCustomAttribute<WriteAsAttribute>();

                if(writeAs != null)
                    result.writeAsInfo = GetDbTypeInfo(writeAs.WriteType);

                try
                {
                    result.selfDbInfo = GetDbTypeInfo(t);
                }
                catch
                {
                    logger.LogWarning($"No self dbinfo for type {t} (this is ok)");
                }

                var allProperties = t.GetProperties().ToDictionary(x => x.Name, x => x);

                foreach(var pk in allProperties)
                {
                    var writeRule = pk.Value.GetCustomAttribute<WritableAttribute>(); 
                    var fieldSelect = pk.Value.GetCustomAttribute<FieldSelectAttribute>()?.SelectField_Sql;

                    var fieldInfo = new ViewFieldInfo()
                    {
                        rawProperty = pk.Value,
                        queryable = !Attribute.IsDefined(pk.Value, typeof(NoQueryAttribute)),
                        multiline = Attribute.IsDefined(pk.Value, typeof(MultilineAttribute)),
                        expensive = pk.Value.GetCustomAttribute<ExpensiveAttribute>()?.PotentialCost ?? 0,
                        onInsert = writeRule?.InsertRule, //null means not writable
                        onUpdate = writeRule?.UpdateRule,
                        fieldSelect = (fieldSelect == "") ? pk.Key : fieldSelect
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