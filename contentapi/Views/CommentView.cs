using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Views
{
    public class CommentView : BaseView, IEditView
    {
        public DateTime createDate {get;set;}
        public DateTime editDate {get;set;}

        public long createUserId {get;set;}
        public long editUserId {get;set;}

        [Required]
        public long parentId {get;set;}

        [Required]
        [StringLength(4096, MinimumLength=2)]
        public string content {get;set;}

        public bool deleted {get;set;}
    }

    public class CommentAggregateView : IIdView
    {
        public long id {get;set;} //This is PARENT id
        public int count {get;set;}
        public DateTime? firstPost {get;set;}
        public DateTime? lastPost {get;set;}
        public long lastId {get;set;}
        public List<long> userIds {get;set;}
    }
}