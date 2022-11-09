namespace contentapi.oldsbs;

public class CommentsHistory : Comments
{
    public string action {get;set;} = "";
    public long revision {get;set;}
    public DateTime revisiondate {get;set;}
    //public long cid {get;set;} //primary key with revision
    //public long? pcid {get;set;}kkkkkkk
}