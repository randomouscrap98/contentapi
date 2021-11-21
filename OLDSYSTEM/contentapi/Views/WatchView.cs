using System;

namespace contentapi.Views
{
    public class WatchView : BaseView, IBaseView
    {
        public DateTime createDate {get;set;}
        public long userId {get;set;}
        public long contentId {get;set;}
        public long lastNotificationId {get;set;}
    }
}