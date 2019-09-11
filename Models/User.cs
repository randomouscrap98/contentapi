using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Models
{
    //This is the user as they are in the database
    [Table("users")]
    public class User : GenericModel
    {
        public string username { get; set; }
        public string email { get; set; }
        public byte[] passwordHash { get; set; }
        public byte[] passwordSalt { get; set; }
        public string registerCode {get;set;}
    }

    //This is the user as we give them out
    public class UserView : GenericView
    {
        public string username { get; set; }
    }

    //This is the user as they sign in (or create account?)
    public class UserCredential
    {
        [MinLength(3)]
        [MaxLength(20)]
        //[Remote("CheckUniqueUsername", "Users", AdditionalFields = "id", ErrorMessage = "Username taken!")]
        public string username {get;set;}

        [Required]
        [MinLength(8)]
        public string password {get;set;}

        [EmailAddress]
        //[Remote("CheckUniqueEmail", "Users", AdditionalFields = ErrorMessage = "Email taken!")]
        public string email {get; set;}
    }
}