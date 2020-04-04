using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace contentapi.Views
{
    //This is the user as we give them out
    public class UserView
    {
        public long id {get;set;}
        public DateTime createDate{get;set;}

        public string username { get; set; }
        public string email { get; set; } //This field SHOULDN'T be set unless the user is ourselves.
    }
}