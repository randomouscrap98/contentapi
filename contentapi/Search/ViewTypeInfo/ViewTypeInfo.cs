
namespace contentapi.Search;

public class ViewTypeInfo
{
    /// <summary>
    /// The type that produced this typeinfo
    /// </summary>
    /// <returns></returns>
    public Type type {get;set;} = typeof(ViewTypeInfo);

    /// <summary>
    /// Note: ANY field defined here is technically "retrievable" in the fields list, as any view is meant to be consumed by a user
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="DbFieldInfo"></typeparam>
    /// <returns></returns>
    public Dictionary<string, ViewFieldInfo> fields {get;set;} = new Dictionary<string, ViewFieldInfo>();

    //These don't necessarily map to direct database things, even though they MOSTLY do
    public RequestType? requestType {get;set;}
    public string selectFromSql {get;set;} = "";
    public string whereSql {get;set;} = "";
    public string groupBySql {get;set;} = "";
    public DbTypeInfo? writeAsInfo {get;set;}
    public DbTypeInfo? selfDbInfo {get;set;}

    public List<string> extraQueryFields {get;set;} = new List<string>();
}
