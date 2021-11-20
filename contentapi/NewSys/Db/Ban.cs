using System;

namespace contentapi.Db
{
    public class Ban
    {
        public long id {get;set;}
        public DateTime createDate {get;set;}
        public DateTime expireDate {get;set;}
        public long createUserId {get;set;}
        public long bannedUserId {get;set;}
        public string message {get;set;}
        public BanType type {get;set;}
    }
}