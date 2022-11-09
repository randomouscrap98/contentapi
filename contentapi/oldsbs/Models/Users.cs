namespace contentapi.oldsbs;

public class Users
{
    public long uid {get;set;}
    public DateTime created {get;set;}
    public string username {get;set;} = "";
    public string password {get;set;} = "";
    public string email {get;set;} = "";
    public long authority {get;set;} //what to do with this?
    public long preferences {get;set;} // and this? user settings?
    public string avatar {get;set;} = ""; // is this a full link???
    public string language {get;set;} = "";
    public long tid {get;set;} // discard
    public long cid {get;set;} // discard
    public string about {get;set;} = ""; // is this a user page now?
    public bool avatarblocked {get;set;} //discard?
}