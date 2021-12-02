namespace contentapi.Search;

[System.AttributeUsage(System.AttributeTargets.Property)]
public class SearchableAttribute : System.Attribute  
{  
    public SearchableAttribute() {  }  
}  

//This is used for properties that you do NOT ever select from the database, they're just 
//'always' available. Not sure if this will work with the new system...
[System.AttributeUsage(System.AttributeTargets.Property)]
public class ComputedAttribute : System.Attribute  
{  
    public ComputedAttribute() {  }  
}  

[System.AttributeUsage(System.AttributeTargets.Property)]
public class FromFieldAttribute : System.Attribute  
{  
    public string Field {get;}
    public FromFieldAttribute(string field) {  this.Field = field; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class FromDbAttribute : System.Attribute  
{  
    public Type Type {get;}
    public FromDbAttribute(Type type) {  this.Type = type; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class ForRequestAttribute : System.Attribute  
{  
    public RequestType Type {get;}
    public ForRequestAttribute(RequestType type) {  this.Type = type; }  
}  