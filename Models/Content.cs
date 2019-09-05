using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace contentapi.Models
{
    [Table("content")]
    public class Content : GenericModel
    {
        public int userId {get;set;}
        public string title {get;set;}
        public string content {get;set;}

        public string baseAccess {get;set;}
        public virtual List<contentAccess>  accessList {get;set;}= new List<contentAccess>();
    }
}