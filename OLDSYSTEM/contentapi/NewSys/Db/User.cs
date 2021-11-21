using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("users")]
    public class User
    {
        [ExplicitKey] //This is only during conversion!
        public long id {get;set;}
        public string username {get;set;}
        public long avatar {get;set;}
        public string special {get;set;}
        public bool super {get;set;}
        public DateTime createDate {get;set;}
        public DateTime editDate {get;set;}
        public string email {get;set;}
        public string hidelist {get;set;}
        public string password {get;set;} //Don't worry, just the salted hash
        public string salt {get;set;}
        public string registrationKey {get;set;}

        [Write(false)]
        public List<long> hideListParsed => 
            hidelist.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x.Trim())).ToList();
    }
}