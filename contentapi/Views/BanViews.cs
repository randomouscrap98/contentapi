using System;

namespace contentapi.Views
{
    public class BanViewBase : BaseView, IBaseView
    {
        public DateTime createDate { get;set;}
        public DateTime expireDate { get;set;}

        public long createUserId {get;set;}
        public long bannedUserId {get;set;}

        public string message {get;set;}
    }

    //Literally exactly the same
    public class PublicBanView : BanViewBase
    {

    }
}