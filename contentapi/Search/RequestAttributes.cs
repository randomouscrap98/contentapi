namespace contentapi.Search;

[System.AttributeUsage(System.AttributeTargets.Field)]
public class ViewMapAttribute : System.Attribute  
{  
    public Type ViewType {get;}
    public ViewMapAttribute(Type type) {  this.ViewType = type; }  
}  