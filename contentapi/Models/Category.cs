using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Models
{
    //This is the user as they are in the database
    [Table("categories")]
    public class Category : GenericAccessModel, IGenericAccessModel
    {
        public long? parentId {get;set;}
        public string name {get;set;}
        public string description {get;set;}
        public string type {get;set;}

        public override List<IGenericSingleAccess> GenericAccessList 
        {
            get { return AccessList.Cast<IGenericSingleAccess>().ToList(); }
        }

        [ForeignKey("parentId")]
        public virtual Category Parent {get;set;}

        public virtual List<Category> Children {get;set;}
        public virtual List<CategoryAccess> AccessList {get;set;}
    }

    [Table("categoryAccess")]
    public class CategoryAccess: GenericSingleAccess
    {
        public long categoryId {get;set;}
        public virtual Category Category {get;set;}
    }

    public class CategoryView : GenericAccessView
    {
        public long? parentId {get;set;}

        [Required]
        [MinLength(1)]
        public string name {get;set;}

        public string description {get;set;}
        public string type {get;set;}
    }
}