using System.Reflection;

namespace contentapi.Search;

public class DbTypeInfo
{
    public Type modelType {get;set;} = typeof(DbTypeInfo);
    public string modelTable {get;set;} = "";
    public Dictionary<string, PropertyInfo> modelProperties {get;set;} = new Dictionary<string, PropertyInfo>();
}