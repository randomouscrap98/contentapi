using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Models
{
    public class GenericModel 
    {
        [Key]
        public long id {get; set;}
        public DateTime createDate{get;set;}
        public long status {get;set;}
    }

    public class GenericView
    {
        public long id {get;set;}
        public DateTime createDate{get;set;}
        public List<string> _links {get;set;} = new List<string>();
    }



    //public class GenericSingleAccess : GenericModel
    //{
    //    public long userId {get;set;}
    //    public string access {get;set;}

    //    public virtual User user {get;set;}
    //}

    //public class GenericAccessModel : GenericModel
    //{
    //    //public string inheritAccess {get;set;} //Some things don't use this, but if you're an access model, anything BELOW you need to inherit access perms

    //    public string baseAccess {get;set;}
    //    public virtual List<GenericSingleAccess>  accessList {get;set;}= new List<GenericSingleAccess>();
    //}

    //public class GenericAccessView : GenericView
    //{
    //    //public string defaultAccess {get;set;}
    //    //0 is "default" (simple interface). What if users get rid of 0? it re-inherits OR ignores the removal maybe.
    //    public Dictionary<long, string>  accessList {get;set;}= new Dictionary<long, string>();
    //}
}