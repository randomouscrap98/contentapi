namespace contentapi.Live;

public class UserStatus
{
    public long userId {get;set;}
    public string status {get;set;} = "";
    public DateTime createDate {get;set;} = DateTime.UtcNow;
    public int trackerId {get;set;}
}