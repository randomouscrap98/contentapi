using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    //This is the user as they sign in (or create account?)
    public class UserCredential
    {
        [MinLength(1, ErrorMessage="Username too short Min: 3!")]
        [MaxLength(20, ErrorMessage="Username too long! Max: 20")]
        public string username {get;set;}

        [MinLength(8, ErrorMessage="Password too short!")]
        public string password {get;set;}

        [EmailAddress]
        public string email {get; set;}
    }

    public class UserAuthenticate : UserCredential
    {
        public int ExpireSeconds {get;set;}
    }

    public class SensitiveUserChange : UserCredential
    {
        public string oldPassword {get;set;}
    }
}