using System;
using System.Collections.Generic;
using AutoMapper;

namespace contentapi.Views
{
    public class ModuleMessageView : IBaseView
    {
        public DateTime createDate {get;set;} // = DateTime.Now;
        public long id {get;set;}
        public string message {get;set;}
        public List<long> usersInMessage {get;set;} = new List<long>();
        public string module {get;set;}
        public long receiveUserId {get;set;} = -1;
        public long sendUserId {get;set;} = -1;
    }

    public class UnifiedModuleMessageView: ModuleMessageView
    {
        public long parentId {get;set;} = 0;
    }

}