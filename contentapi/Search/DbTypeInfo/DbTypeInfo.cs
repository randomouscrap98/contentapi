
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
    //public Dictionary<string, System.Reflection.PropertyInfo> properties {get;set;} = new Dictionary<string, System.Reflection.PropertyInfo>();

    /// <summary>
    /// Fields the user is allowed to place in the "query"  
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    //public List<string> searchableFields {get;set;} = new List<string>();

    /// <summary>
    /// Fields the user is allowed to request as output
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    //public List<string> queryableFields {get;set;} = new List<string>();
    //public Dictionary<string, Type> fieldTypes {get;set;} = new Dictionary<string, Type>();
    //public Dictionary<string, string> fieldRemap {get;set;} = new Dictionary<string, string>();

    //These don't necessarily map to direct database things, even though they MOSTLY do
    public RequestType? requestType {get;set;}
    public Type? modelType {get;set;}
    public string? modelTable {get;set;}
    //Note: the modelProperties, although MOSTLY mapped to fields, aren't NECESSARILY mapped to fields, so we must have it be a separate list
    public Dictionary<string, System.Reflection.PropertyInfo> modelProperties {get;set;} = new Dictionary<string, System.Reflection.PropertyInfo>();

    /// <summary>
    /// ANY field defined in a view is retrievable, however this may change in the future, so please try to use this function instead.
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    //public bool IsFieldRetrievable(string field) => fields.ContainsKey(field);

}
