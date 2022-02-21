namespace contentapi.Search;

/// <summary>
/// This attribute describes which request type should produce results in the form of this class
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class ResultForAttribute : System.Attribute
{
    public RequestType Type {get;}
    public ResultForAttribute(RequestType type) {  this.Type = type; }  
}

/// <summary>
/// This attribute describes the literal "from (tables + join)" used for this class when trying
/// to form a query for this class
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class SelectFromAttribute : System.Attribute  
{  
    public string SelectFrom_Sql {get;}
    public SelectFromAttribute(string sql) {  this.SelectFrom_Sql = sql; }  
}  

/// <summary>
/// This attribute describes how to pull this particular field from the database when
/// trying to form a query. This may reference names/structures used in the SelectFrom attribute.
/// If this attribute is not provided, it is ASSUMED that the field is specifically
/// NOT queryable using the default simple query system. However, this does NOT imply
/// the ability of a field to be retrieved or used in a query by a user. An empty attribute
/// (or empty string) means use the field's name as the selector, a simple select
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property)]
public class FieldSelectAttribute : System.Attribute  
{  
    public string SelectField_Sql {get;}
    public FieldSelectAttribute(string sql = "") {  this.SelectField_Sql = sql; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class WhereAttribute : System.Attribute  
{  
    public string Where_Sql {get;}
    public WhereAttribute(string sql = "") {  this.Where_Sql = sql; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class GroupByAttribute : System.Attribute  
{  
    public string GroupBy_Sql {get;}
    public GroupByAttribute(string sql = "") {  this.GroupBy_Sql = sql; }  
}  

[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = true)]
public class ExtraQueryFieldAttribute : System.Attribute  
{  
    public string field {get;set;}
    public string? select {get;set;}
    public ExtraQueryFieldAttribute(string field, string? select = null) 
    { 
       this.field = field;
       this.select = select;
    } 
}  

/// <summary>
/// This attribute dictates whether a field can be used inside a user's "query" statement.
/// Just because a field can be pulled doesn't mean it's fit to be used in a WHERE clause.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property)]
public class NoQueryAttribute : System.Attribute  
{  
    public NoQueryAttribute() {  }  
}  

/// <summary>
/// Rule types for expressing how a field is maintained on insert or update. There are no "null" or
/// default/readonly rule types, because it is assumed that any field without a defined write rule 
/// is in fact readonly (fields default to readonly)
/// </summary>
public enum WriteRule
{
    /// <summary>
    /// Accept whatever the user supplies
    /// </summary>
    User,
    /// <summary>
    /// Keep whatever was in the database. If there is no value, the default value for the type is chosen
    /// </summary>
    Preserve,
    /// <summary>
    /// Automatically assign the user id to the one who is performing the write request
    /// </summary>
    AutoUserId,
    /// <summary>
    /// Automatically assign the date to the current UTC date
    /// </summary>
    AutoDate,
    /// <summary>
    /// Add one every time the value is written. On initial write, the value is probably 0
    /// </summary>
    Increment
}

[System.AttributeUsage(System.AttributeTargets.Property)]
public class WritableAttribute : System.Attribute  
{  
    public WriteRule InsertRule {get;}
    public WriteRule UpdateRule {get;}
    public WritableAttribute(WriteRule insertRule = WriteRule.User, WriteRule updateRule = WriteRule.User) 
    { 
        this.InsertRule = insertRule; 
        this.UpdateRule = updateRule; 
    }  
}  

/// <summary>
/// This attribute describes which database type to write the view as.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
public class WriteAsAttribute : System.Attribute  
{  
    public Type WriteType {get;}
    public WriteAsAttribute(Type type) {  this.WriteType = type; }  
}  

// The rest of these help users understand fields, but do not impact queries or code.

[System.AttributeUsage(System.AttributeTargets.Property)]
public class ExpensiveAttribute : System.Attribute
{
    public int PotentialCost {get;}
    public ExpensiveAttribute(int cost) { this.PotentialCost = cost; }
}

[System.AttributeUsage(System.AttributeTargets.Property)]
public class MultilineAttribute : System.Attribute  
{  
    public MultilineAttribute() {  }  
}  
