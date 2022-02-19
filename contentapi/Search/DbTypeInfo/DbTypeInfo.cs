
namespace contentapi.Search;

public class DbTypeInfo
{
    /// <summary>
    /// The type that produced this typeinfo
    /// </summary>
    /// <returns></returns>
    public Type type {get;set;} = typeof(DbTypeInfo);

    /// <summary>
    /// Note: ANY field defined here is technically "retrievable" in the fields list, as any view is meant to be consumed by a user
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="DbFieldInfo"></typeparam>
    /// <returns></returns>
    public Dictionary<string, DbFieldInfo> fields {get;set;} = new Dictionary<string, DbFieldInfo>();

    //These don't necessarily map to direct database things, even though they MOSTLY do
    public RequestType? requestType {get;set;}
    public string selectFromSql {get;set;} = "";
    public string whereSql {get;set;} = "";

    //public Type? modelType {get;set;}
    //public string? modelTable {get;set;}
    //Note: the modelProperties, although MOSTLY mapped to fields, aren't NECESSARILY mapped to fields, so we must have it be a separate list
    //public Dictionary<string, System.Reflection.PropertyInfo> modelProperties {get;set;} = new Dictionary<string, System.Reflection.PropertyInfo>();

}
