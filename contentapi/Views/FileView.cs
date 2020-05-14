using System;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class FileView : BasePermissionView, IEditView, IPermissionView, IValueVlue
    {
        public long parentId { get; set; }

        public DateTime createDate { get; set;}
        public DateTime editDate { get;set;}
        public long createUserId { get;set;} 
        public long editUserId { get;set;}

        public string myPerms { get;set;}

        [MaxLength(128)]
        public string name {get;set;}

        //This is ignored if changed by the user.
        public string fileType {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (FileView)obj;
            return base.EqualsSelf(obj) && o.name == name && o.fileType == fileType;
        }
    }
}