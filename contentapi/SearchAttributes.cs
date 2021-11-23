namespace contentapi;

[System.AttributeUsage(System.AttributeTargets.Property)]
public class SearchableAttribute : System.Attribute  
{  
    public SearchableAttribute() {  }  
}  

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