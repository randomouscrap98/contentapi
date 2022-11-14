namespace contentapi.oldsbs;

//Fully converted as standard json history
//We're only going to keep this for archival purposes; i'm not going to attempt to recreate the activity 
//by trying to get all the create/etc events with historic content into the tables. SO, these will most
//likely end up being messages on some system content again. 
public class ForumThreadsHistory : ForumThreads
{
    public string action {get;set;} = "";
    public long revision {get;set;}
    public DateTime revisiondate {get;set;}
}