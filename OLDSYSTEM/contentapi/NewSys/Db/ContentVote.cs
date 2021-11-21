using System;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content_votes")]
    public class ContentVote
    {
        [Key]
        public long id {get;set;}
        public long contentId {get;set;}
        public long userId {get;set;}
        public VoteType vote {get;set;}
        public DateTime createDate {get;set;}
    }
}