using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace contentapi.Models
{
    [Table("subcontentEntities")]
    public class SubcontentEntity : EntityChild
    {
        public string content {get;set;}
        public string format {get;set;}
        public long contentId {get;set;}

        public virtual ContentEntity Content {get;set;}
    }

    public class SubcontentView : EntityView
    {
        public string content {get;set;}
        public string format {get;set;}
        public long contentId {get;set;}
    }
}