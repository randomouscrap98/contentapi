namespace contentapi.oldsbs;

public class Comments  
{
    public long cid {get;set;} //primary key
    public long? pcid {get;set;} //comments can have parents (careful)
    public long pid {get;set;} //parent page id
    public long uid {get;set;}
    public long? euid {get;set;}
    public DateTime created {get;set;} //this isn't normally null but...
    public DateTime? edited {get;set;}
    public string content {get;set;} = "";
    public long status {get;set;} //once again, they are ALL zero. Is this normal???
}