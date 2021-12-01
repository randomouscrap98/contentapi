using contentapi.Search;

namespace contentapi.Utilities;

public class TypeInfo
{
    /// <summary>
    /// The type that produced this typeinfo
    /// </summary>
    /// <returns></returns>
    public Type type {get;set;} = typeof(TypeInfo);

    /// <summary>
    /// Fields the user is allowed to place in the "query"  
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    public List<string> searchableFields {get;set;} = new List<string>();

    /// <summary>
    /// Fields the user is allowed to request as output
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <returns></returns>
    public List<string> queryableFields {get;set;} = new List<string>();
    public Dictionary<string, Type> fieldTypes {get;set;} = new Dictionary<string, Type>();
    public Dictionary<string, string> fieldRemap {get;set;} = new Dictionary<string, string>();

    //These don't necessarily map to direct database things, even though they MOSTLY do
    public RequestType? requestType {get;set;}
    public Type? dbType {get;set;}
    public string? database {get;set;}
}