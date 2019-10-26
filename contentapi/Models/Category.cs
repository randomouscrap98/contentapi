using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Models
{
    [Table("categoryEntities")]
    public class CategoryEntity : EntityChild
    {
        public long? parentId {get;set;}
        public string name {get;set;}
        public string description {get;set;}
        public string type {get;set;}


        [ForeignKey("parentId")]
        public virtual CategoryEntity Parent {get;set;}

        public virtual List<CategoryEntity> Children {get;set;}
    }

    public class CategoryView : EntityView
    {
        public long? parentId {get;set;}

        [Required]
        [MinLength(1)]
        public string name {get;set;}
        public string description {get;set;}
        public string type {get;set;}
    }
}