using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content_keywords")]
    public class ContentKeyword
    {
        [Key]
        public long id {get;set;}
        public long contentId {get;set;}
        public string value {get;set;}
    }
}