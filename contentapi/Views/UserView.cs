
using System;

namespace contentapi.Views
{
    /// <summary>
    /// The user view most people see: the minimal amount of data
    /// </summary>
    public class UserViewBasic : BaseView
    {
        public string username { get; set; }
        public long avatar {get;set;}
        public DateTime createDate { get; set; }
        public string special {get;set;}
    }

    /// <summary>
    /// The user view only the owner sees: it has their email, etc that other people shouldn't see
    /// </summary>
    public class UserView : UserViewBasic, IEditView
    {
        public DateTime editDate { get;set;}
        public long createUserId { get;set;} 
        public long editUserId { get;set;}

        public string email { get; set; } //This field SHOULDN'T be set unless the user is ourselves.

        //This is actually GET only, don't use it during compare.
        public bool super { get;set; }

        protected override bool EqualsSelf(object obj)
        {
            var o = (UserView)obj;
            return base.EqualsSelf(obj) && o.username == username && o.avatar == avatar && o.email == email;
        }
    }

    /// <summary>
    /// The user view that ONLY the system uses. There should never be a way to retrieve the salt, password hash, etc.
    /// </summary>
    public class UserViewFull : UserView
    {
        public string password {get;set;}
        public string salt {get;set;}
        public string registrationKey {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (UserViewFull)obj;
            return base.EqualsSelf(obj) && o.password == password && o.salt == salt && o.registrationKey == registrationKey;
        }
    }
}