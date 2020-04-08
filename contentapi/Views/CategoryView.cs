using System.Collections.Generic;

namespace contentapi.Views
{
    public class CategoryView : ViewBase
    {
        public string name {get;set;}
        public string description {get;set;}

        //The creator of the category
        public long userId {get;set;}

        //The direct parent of the category (only one)
        public long parentId {get;set;}

        public Dictionary<long, string> permissions {get;set;} = new Dictionary<long, string>();
    }
}