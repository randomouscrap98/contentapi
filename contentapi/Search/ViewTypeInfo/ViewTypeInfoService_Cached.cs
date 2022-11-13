using System.Reflection;
using contentapi.data;
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
                var extraQueryAttributes = t.GetCustomAttributes<ExtraQueryFieldAttribute>() ?? new List<ExtraQueryFieldAttribute>();

                var result = new Search.ViewTypeInfo() { 
                    type = t,
                    selectFromSql = t.GetCustomAttribute<SelectFromAttribute>()?.SelectFrom_Sql ?? "",
                    whereSql = t.GetCustomAttribute<WhereAttribute>()?.Where_Sql ?? "",
                    groupBySql = t.GetCustomAttribute<GroupByAttribute>()?.GroupBy_Sql ?? "",
                    extraQueryFields = extraQueryAttributes.ToDictionary(x => x.field, y => y.select ?? y.field),
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
                    var dbField = pk.Value.GetCustomAttribute<DbField>();
                    var ignored = pk.Value.GetCustomAttribute<FullIgnoreAttribute>();

                    if(ignored != null)
                        continue;

                    var fieldInfo = new ViewFieldInfo()
                    {
                        rawProperty = pk.Value,
                        queryable = !Attribute.IsDefined(pk.Value, typeof(NoQueryAttribute)),
                        multiline = Attribute.IsDefined(pk.Value, typeof(MultilineAttribute)),
                        expensive = pk.Value.GetCustomAttribute<ExpensiveAttribute>()?.PotentialCost ?? 0,
                        onInsert = writeRule?.InsertRule ?? WriteRule.Preserve, //Preserve is safer, basically "readonly"
                        onUpdate = writeRule?.UpdateRule ?? WriteRule.Preserve,
                        fieldSelect = (dbField?.SelectAlias == "") ? pk.Key : dbField?.SelectAlias,
                        //fieldWhere is whatever the PROPERTY name is. This is an extremely safe default, as all selects
                        //are aliased to the name of the column. Only time you need to modify this is when there's an ambiguity in the column alias
                        fieldWhere = (dbField?.WhereAlias == "") ? pk.Key : dbField?.WhereAlias
                    };

                    //The column is assumed to be whatever the select is. This only works if you're just renaming a column, which is the standard
                    //case. If you're performing a complex select for a column, it's almost NEVER written to anyway.
                    fieldInfo.fieldColumn = (dbField?.DbColumnAlias == "") ? fieldInfo.fieldSelect : dbField?.DbColumnAlias;

                    result.fields.Add(pk.Key, fieldInfo);
                }

                logger.LogInformation($"Added type {t.Name} to cached type service");
                cachedTypes.Add(t, result);
            }
        }
        return cachedTypes[t];
    }
}