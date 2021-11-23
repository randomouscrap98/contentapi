namespace contentapi;

public class TypeInfo
{
    public Type type {get;set;} = typeof(TypeInfo);
    public List<string> searchableFields {get;set;} = new List<string>();
    public List<string> queryableFields {get;set;} = new List<string>();
    public Dictionary<string, Type> fieldTypes {get;set;} = new Dictionary<string, Type>();
    public Dictionary<string, string> fieldRemap {get;set;} = new Dictionary<string, string>();
    public Type? dbType {get;set;}
    public string? database {get;set;}
}