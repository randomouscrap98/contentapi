using System.Collections.Generic;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class BaseParentSearch : EntitySearchBase {
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class BaseContentSearch : BaseParentSearch {
        public string Name {get;set;}
    }

    public class FileSearch : BaseContentSearch { }
    public class CategorySearch : BaseContentSearch { }
    public class ContentSearch : BaseContentSearch
    {
        public string Keyword {get;set;}
        public string Type {get;set;}
    }


    public class CommentSearch : BaseParentSearch
    {
        public List<long> UserIds {get;set;}
    }

    //Bellow are special searches that derive from nothing but the base entity search

    public class ActivitySearch : EntitySearchBase
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();

        public string Type {get;set;}
    }

    public class UserSearch : EntitySearchBase
    {
        public string Username {get;set;}
    }

}