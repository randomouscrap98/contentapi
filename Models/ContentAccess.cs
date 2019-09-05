using System.ComponentModel.DataAnnotations.Schema;

namespace contentapi.Models
{
    //[Table("categoryAccess")]
    //public class CategoryAccess : GenericSingleAccess
    //{
    //    public int categoryId;
    //}

    [Table("contentAccess")]
    public class contentAccess: GenericModel
    {
        public long userId {get;set;}
        public string access {get;set;}
        public int contentId {get;set;}

        public virtual User user {get;set;}
        public virtual Content content {get;set;}
    }
}