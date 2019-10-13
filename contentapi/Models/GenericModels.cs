using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Models
{
    // **************
    // * Interfaces *
    // **************

    /*public interface IGenericModel
    {
        long id {get; set;}
        DateTime createDate{get;set;}
        long status {get;set;}
    }

    public interface IGenericSingleAccess : IGenericModel
    {
        long userId {get;set;}
        string access {get;set;}

        User User {get;set;}
    }

    public interface IGenericAccessModel : IGenericModel
    {
        string baseAccess {get;set;}
        List<IGenericSingleAccess>  GenericAccessList {get;}
    }

    // **********
    // * Models *
    // **********

    public class GenericModel : IGenericModel
    {
        [Key]
        public long id {get; set;}
        public DateTime createDate{get;set;}
        public long status {get;set;}
    }

    public class GenericAccessModel : GenericModel, IGenericAccessModel
    {
        public string baseAccess {get;set;}
        public virtual List<IGenericSingleAccess>  GenericAccessList {get;} = new List<IGenericSingleAccess>();
    }

    public class GenericSingleAccess : GenericModel, IGenericSingleAccess
    {
        public long userId {get;set;} 
        public string access {get;set;}
        public virtual User User {get;set;}
    }

    // *********
    // * Views *
    // *********
    public class GenericView
    {
        public long id {get;set;}
        public DateTime createDate{get;set;}
        public List<string> _links {get;set;} = new List<string>();
    }

    public class GenericAccessView : GenericView
    {
        public string baseAccess {get;set;}
        public Dictionary<long, string>  accessList {get;set;}= new Dictionary<long, string>();
    }*/
}