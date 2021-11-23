namespace contentapi;

public class TypeInfo
{
    public Type type {get;set;} = typeof(TypeInfo);
    public List<string> searchableFields {get;set;} = new List<string>();
    public List<string> queryableFields {get;set;} = new List<string>();
}