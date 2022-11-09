namespace contentapi.oldsbs;

public class MessageRecipients
{
    public long mid {get;set;} //primary key with recipient (message)
    public long recipient {get;set;}
    public long status {get;set;}
}