using System;

namespace contentapi.Db
{
    public class Content
    {
        public long id {get;set;}
        public bool deleted {get;set;}
        public DateTime createDate {get;set;}
        public InternalContentType internalType {get;set;}
        public string publicType {get;set;}
        public string name {get;set;}
        public string content {get;set;}
        public long? parentId {get;set;}
    }
}