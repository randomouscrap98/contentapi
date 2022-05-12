using System;
using contentapi.data;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("content")]
    public class Content
    {
        [Key]
        public virtual long id { get; set; }
        public bool deleted { get; set; }
        public long createUserId { get; set; }
        public DateTime createDate { get; set; }
        public InternalContentType contentType { get; set; }
        public string name { get; set; } = "";
        public string text { get; set; } = "";
        public long parentId { get; set; }

        //Some special new types 
        public string? literalType {get;set;}     //The page type set by users, OR the file mimetype
        public string? meta {get;set;}           //Not always used, READONLY after insert
        public string? description {get;set;}    //Tagline for pages, description for anything else maybe
        public string? hash {get;set;}           //Some kind of unique public identifier. Uniqueness is enforced by the API however
    }

    [Table("content")]
    public class Content_Convert : Content
    {
        [ExplicitKey]
        public override long id {get;set;}
    }
}
