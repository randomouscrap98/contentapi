using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace contentapi.Models
{
    [Table("commentEntities")]
    public class CommentEntity : EntityChild
    {
        public string content {get;set;}
        public string format {get;set;}
        public long parentId {get;set;}

        [ForeignKey(nameof(parentId))]
        public virtual Entity ParentEntity {get;set;}
    }

    public class CommentView : EntityView
    {
        public string content {get;set;}
        public string format {get;set;}
        public long parentId {get;set;}
    }
}