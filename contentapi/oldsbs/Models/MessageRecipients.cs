namespace contentapi.oldsbs;

public class MessageRecipients
{
    public long mid {get;set;} //primary key with recipient (message)
    public long recipient {get;set;}
    public long status {get;set;} //1: read, 2:deleted, 4: replied
}