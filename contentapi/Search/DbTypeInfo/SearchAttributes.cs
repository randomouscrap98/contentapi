namespace contentapi.Search;


[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class FromTableAttribute : System.Attribute  
{  
    public Type Type {get;}
    public FromTableAttribute(Type type) {  this.Type = type; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class ForRequestAttribute : System.Attribute  
{  
    public RequestType Type {get;}
    public ForRequestAttribute(RequestType type) {  this.Type = type; }  
}  


// The rest are attributes that describe fields. They MAY be all brought together
// under a single object

[System.AttributeUsage(System.AttributeTargets.Property)]
public class FromFieldAttribute : System.Attribute  
{  
    public string Field {get;}
    public FromFieldAttribute(string field) {  this.Field = field; }  
}  

[System.AttributeUsage(System.AttributeTargets.Property)]
public class SearchableAttribute : System.Attribute  
{  
    public SearchableAttribute() {  }  
}  

[System.AttributeUsage(System.AttributeTargets.Property)]
public class ReadOnlyAttribute : System.Attribute  
{  
    public ReadOnlyAttribute() {  }  
}  

[System.AttributeUsage(System.AttributeTargets.Property)]
public class ExpensiveAttribute : System.Attribute
{
    public int PotentialCost {get;}
    public ExpensiveAttribute(int cost) { this.PotentialCost = cost; }
}

//I don't know about these...

//This is used for properties that you do NOT ever select from the database, they're just 
//'always' available. Not sure if this will work with the new system...
[System.AttributeUsage(System.AttributeTargets.Property)]
public class ComputedAttribute : System.Attribute  
{  
    public ComputedAttribute() {  }  
}  
