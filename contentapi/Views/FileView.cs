using System;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class FileView : StandardView
    {
        [MaxLength(128)]
        public string name {get;set;}

        public string bucket {get;set;}

        //These are ignored if changed by the user.
        public string fileType {get;set;}
        //public string readonlyKey {get;set;}

        protected override bool EqualsSelf(object obj)
        {
            var o = (FileView)obj;
            return base.EqualsSelf(obj) && o.name == name && o.fileType == fileType;
        }
    }
}