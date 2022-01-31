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

public enum WriteRuleType
{
    None = 0,
    Preserve,
    AutoUserId,
    AutoDate,
    DefaultValue,
    /// <summary>
    /// Note on readonly: it's used mostly for fields tracked internally, and isn't STRICTLY required, but makes things easier for everyone.
    /// For instance, fields that have no database backing are by default unwritable, so readonly is technically useless on those fields, but
    /// it's a good indicator to use when a field is tracked with the system rather than users, just not with one of the other attributes. 
    /// It's a "catch all"; these fields are readonly only to the USER
    /// </summary>
    ReadOnly,
    Increment
}

[System.AttributeUsage(System.AttributeTargets.Property)]
public class WriteRuleAttribute : System.Attribute  
{  
    public WriteRuleType InsertRule {get;}
    public WriteRuleType UpdateRule {get;}
    public WriteRuleAttribute(WriteRuleType insertRule, WriteRuleType updateRule = WriteRuleType.Preserve) { this.InsertRule = insertRule; this.UpdateRule = updateRule; }  
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
