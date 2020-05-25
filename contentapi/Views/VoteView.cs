using System;

namespace contentapi.Views
{
    public class VoteView : BaseView, IBaseView
    {
        public string vote {get;set;}
        public DateTime createDate {get;set;}
        public long userId {get;set;}
        public long contentId {get;set;}
    }
}