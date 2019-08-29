using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace contentapi.Models
{
    //This is the user as they are in the database
    [Table("categories")]
    public class Category : GenericModel
    {
        public long? parentId {get;set;}
        public string name {get;set;}
        public string description {get;set;}
        public string accessPerms {get;set;}
        public string defaultPerms {get;set;}

        [ForeignKey("parentId")]
        public virtual Category Parent {get;set;}
        [InverseProperty(nameof(Parent))]
        public virtual List<Category> Children {get;set;}
    }

    public class CategoryView : GenericModel
    {
        public long? parentId {get;set;}
        [Required]
        [MinLength(1)]
        public string name {get;set;}
        public string description {get;set;}
        [Required(AllowEmptyStrings = true)]
        public string accessPerms {get;set;}
        [Required(AllowEmptyStrings = true)]
        public string defaultPerms {get;set;}
        public List<long> childrenIds {get;set;}
    }
}