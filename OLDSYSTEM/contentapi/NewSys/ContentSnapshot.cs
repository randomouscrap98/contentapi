using System.Collections.Generic;
using contentapi.Db;

namespace contentapi
{
    public class ContentSnapshot : Content
    {
        public List<ContentValue> values {get;set;}
        public List<ContentKeyword> keywords {get;set;}
        public List<ContentPermission> permissions {get;set;}
    }

}