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