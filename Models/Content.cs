using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace contentapi.Models
{
    [Table("content")]
    public class Content : GenericModel
    {
        public long userId {get;set;}
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}
        public string baseAccess {get;set;}

        public virtual User User {get; set;}
        public virtual List<ContentAccess>  accessList {get;set;}= new List<ContentAccess>();
    }

    public class ContentView : GenericView
    {
        public long userId {get;set;}
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}

        public string baseAccess {get;set;}
        public Dictionary<long, string> accessList {get;set;}
    }
}