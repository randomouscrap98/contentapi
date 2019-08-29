
using System;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Models
{
    public class GenericModel 
    {
        [Key]
        public long id {get; set;}
        public DateTime createDate{get;set;}
    }
}