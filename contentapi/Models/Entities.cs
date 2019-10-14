using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace contentapi.Models
{
    [Flags]
    public enum EntityStatus
    {
        Deleted = 1
    }

    [Flags]
    public enum EntityAction
    {
        None = 0,
        View = 1,
        Create = 2,
        Read = 4,
        Update = 8,
        Delete = 16
    }

    [Table("entities")]
    public class Entity
    {
        public long id {get;set;}
        public DateTime createDate {get;set;}
        public EntityStatus status {get;set;}
        public long? userId {get;set;}
        public EntityAction baseAllow {get;set;}

        public virtual User User {get;set;}
        public virtual List<EntityAccess> AccessList {get;set;}
        public virtual List<EntityLog> Log {get;set;}

        public DateTime? LastEditDate
        {
            get { return Log.Where(x => x.action == EntityAction.Update).OrderByDescending(x => x.createDate).FirstOrDefault()?.createDate; }
        }
    }

    public class EntityChild
    {
        [Key]
        public long entityId {get;set;}

        public virtual Entity Entity {get;set;}
    }

    [Table("entityAccess")]
    public class EntityAccess
    {
        public long id {get;set;}
        public long entityId {get;set;}
        public long userId {get;set;}
        public EntityAction allow {get;set;}
        public DateTime createDate {get;set;}

        public virtual User User {get;set;}
        public virtual Entity Entity {get;set;}
    }

    [Table("entityLog")]
    public class EntityLog
    {
        public long id {get;set;}
        public long entityId {get;set;}
        public long userId {get;set;}
        public EntityAction action {get;set;}
        public DateTime createDate {get;set;}

        public virtual User User {get;set;}
        public virtual Entity Entity {get;set;}
    }

    public class EntityView
    {
        public long id {get;set;}
        public DateTime createDate{get;set;}
        public long? userId {get;set;}
        public string baseAccess {get;set;}
        public Dictionary<long, string>  accessList {get;set;}= new Dictionary<long, string>();

        public List<string> _links {get;set;} = new List<string>();
    }
}