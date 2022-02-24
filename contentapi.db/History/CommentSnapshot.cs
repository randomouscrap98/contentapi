using System;
using System.Collections.Generic;

namespace contentapi.Db.History
{
    public class CommentSnapshot : Comment
    {
        //public //Dictionary<string, string> values {get;set;} = new Dictionary<string, string>();
        public List<CommentValue> values {get;set;} = new List<CommentValue>();
    }
}