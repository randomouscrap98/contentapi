using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace contentapi.Models
{
    [Table("contentEntities")]
    public class ContentEntity : EntityChild
    {
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}
        public string type {get;set;}
        public long categoryId {get;set;}

        public virtual CategoryEntity Category {get;set;}
    }

    public class ContentView : EntityView
    {
        public string title {get;set;}
        public string content {get;set;}
        public string format {get;set;}
        public string type {get;set;}
        public long categoryId {get;set;}
    }
}