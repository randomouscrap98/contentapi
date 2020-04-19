using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace contentapi.Views
{
    public class UserViewBasic : BaseView
    {
        public string username { get; set; }
    }

    //This is the user as we give them out
    public class UserView : UserViewBasic
    {
        public string email { get; set; } //This field SHOULDN'T be set unless the user is ourselves.
        public bool super { get;set; }
    }

    public class UserViewFull : UserView
    {
        public string password {get;set;}
        public string salt {get;set;}
        public string registrationKey {get;set;}
    }
}