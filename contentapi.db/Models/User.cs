using System;
using System.Collections.Generic;
using System.Linq;
using Dapper.Contrib.Extensions;

namespace contentapi.Db
{
    [Table("users")]
    public class User
    {
        [Key]
        public virtual long id { get; set; }
        public string username { get; set; } = "";
        public string avatar { get; set; } = "0";
        public string special { get; set; } //Special is nullable!
        public bool super { get; set; }
        public bool deleted { get; set; }
        public UserType type { get; set; } = UserType.user;
        public DateTime createDate { get; set; }
        public DateTime editDate { get; set; }
        public DateTime lastPasswordDate {get;set;}
        public long createUserId {get;set;}
        public string email { get; set; } = "";
        public string password { get; set; } = ""; //Don't worry, just the salted hash
        public string salt { get; set; } = "";
        public string registrationKey { get; set; } //Registration key is nullable!
    }

    [Table("users")]
    public class User_Convert : User
    {
        [ExplicitKey]
        public override long id { get; set; }
    }
}
