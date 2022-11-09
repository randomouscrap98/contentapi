namespace contentapi.oldsbs;

//these are private messages
public class Messages
{
    public long mid {get;set;} //primary key
    public long sender {get;set;}
    public string content {get;set;} = "";
    public DateTime senddate {get;set;}
}