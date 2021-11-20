using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content_permissions")]
    public class ContentPermission
    {
        [Key]
        public long id {get;set;}
        public long contentId {get;set;}
        public long userId {get;set;}
        public bool create {get;set;}
        public bool read {get;set;}
        public bool update {get;set;}
        public bool delete {get;set;}
    }
}