using System;
using System.Collections.Generic;

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
}