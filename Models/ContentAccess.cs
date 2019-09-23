using System.ComponentModel.DataAnnotations.Schema;

namespace contentapi.Models
{
    [Table("contentAccess")]
    public class ContentAccess: GenericModel
    {
        public long userId {get;set;}
        public string access {get;set;}
        public int contentId {get;set;}

        public virtual User User {get;set;}
        public virtual Content Content {get;set;}
    }
}