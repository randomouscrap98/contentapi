namespace contentapi.oldsbs;

//Fully converted as system content, just plain permissions 
public class ForumCategories
{
    public long fcid {get;set;} //primary key
    public string name {get;set;} = "";
    public string description {get;set;} = "";
    public long permissions {get;set;} //what is this? It's all 0's, so i'm not worried about it
}