using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    //This is the user as they sign in (or create account?)
    public class UserCredential
    {
        [MinLength(3, ErrorMessage="Username too short Min: 3!")]
        [MaxLength(20, ErrorMessage="Username too long! Max: 20")]
        public string username {get;set;}

        [Required]
        [MinLength(8, ErrorMessage="Password too short!")]
        public string password {get;set;}

        [EmailAddress]
        public string email {get; set;}
    }
}