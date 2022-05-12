using System.Collections.Generic;
using contentapi.Db;

namespace contentapi.History;

public class ContentSnapshot : Content
{
    //Definitely want all these immediately usable for adding/etc 
    public List<ContentValue> values {get;set;} = new List<ContentValue>();
    public List<ContentKeyword> keywords {get;set;} = new List<ContentKeyword>();
    public List<ContentPermission> permissions {get;set;} = new List<ContentPermission>();
}