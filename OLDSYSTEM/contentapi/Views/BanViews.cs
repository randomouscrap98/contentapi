using System;
//using System.Text.Json.Serialization;
//using Newtonsoft.Json.Converters;

namespace contentapi.Views
{
    public enum BanType 
    {
        none = 0,
        @public = 1
    }

    public class BanView : BaseView, IBaseView
    {
        public DateTime createDate { get;set;}
        public DateTime expireDate { get;set;}

        public long createUserId {get;set;}
        public long bannedUserId {get;set;}

        //Oops, very specific
        //[JsonConverter(typeof(StringEnumConverter))]
        public BanType type {get;set;}

        public string message {get;set;}
    }

    //Literally exactly the same
    //public class PublicBanView : BanViewBase
    //{

    //}
}