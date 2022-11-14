namespace contentapi.oldsbs;

//Fully converted as part of badges, each badge goes in one group, so this becomes the parentId
//Just a link table, probably not necessary... although I think
//some badges are in multiple groups??? oops, how will that work
// with singular parent? just check I guess...might require values in badge
//NEW: groupsforbadges only has singular relationships, so badges CAN simply
//be children of their parent
public class GroupsForBadges
{
    public long bid {get;set;}
    public long bgid {get;set;}
}