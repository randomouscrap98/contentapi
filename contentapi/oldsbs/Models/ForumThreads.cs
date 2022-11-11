namespace contentapi.oldsbs;

//Any content needs:
// - Global perms (a function exists to generate those)
// - Self perms (won't get generated unless you ask)
// - old fields as values (but how to preserve on update? do we need to?)
public class ForumThreads
{
    public long ftid {get;set;} //primary key
    public long fcid {get;set;} //category (parent)
    public long uid {get;set;}
    public string title {get;set;} = "";
    public DateTime created {get;set;}
    public long views {get;set;}
    public long status {get;set;} //what is this?? might be pinned, locked, etc
    //1 = Important (alert? a special flag, set a value)
    //2 = Sticky (set field on parent; mayhaps should do this after both sets are inserted?)
    //4 = closed (locked? global permissions thing)

    //- Locked threads can simply be threads that have global create removed.
    //- Pinned threads can be a list on the parent category, so that truly only super users can pin stuff
    //NOTE: WILL NEED TO LOOK AT SOURCE CODE TO CONVERT STATUS TO THREAD STUFF
}