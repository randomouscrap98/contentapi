using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace contentapi.Models
{
    //This is the user as they are in the database
    [Table("users")]
    public class User
    {
        public long id { get; set; }
        public DateTime createDate { get; set; }
        public string username { get; set; }
        public string email { get; set; }
        public byte[] passwordHash { get; set; }
        public byte[] passwordSalt { get; set; }
    }

    //This is the user as we give them out
    public class UserView
    {
        public long id { get; set; }
        public DateTime createDate { get; set; }
        public string username { get; set; }
    }

    //This is the user as they sign in (or create account?)
    public class UserCredential
    {
        [MinLength(3)]
        [MaxLength(20)]
        public string username {get;set;}

        [Required]
        [MinLength(8)]
        public string password {get;set;}

        [EmailAddress]
        public string email {get; set;}
    }
}