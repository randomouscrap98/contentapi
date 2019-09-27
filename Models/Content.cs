using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace contentapi.Models
{
    [Table("content")]
    public class Content : GenericAccessModel, IGenericAccessModel
    {
        public long userId {get;set;}
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}
        public string type {get;set;}
        public long categoryId {get;set;}

        public override List<IGenericSingleAccess> GenericAccessList 
        {
            get { return AccessList.Cast<IGenericSingleAccess>().ToList(); }
        }

        public virtual Category Category {get;set;}
        public virtual User User {get; set;}
        public virtual List<ContentAccess> AccessList {get;set;}
    }

    [Table("contentAccess")]
    public class ContentAccess: GenericSingleAccess
    {
        public long contentId {get;set;}
        public virtual Content Content {get;set;}
    }

    public class ContentView : GenericAccessView
    {
        public long userId {get;set;}
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}
        public string type {get;set;}
        public long categoryId {get;set;}
    }
}