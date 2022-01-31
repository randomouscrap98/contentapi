using contentapi.Search;

namespace contentapi.Views;

[FromTable(typeof(Db.UserVariable))]
[ForRequest(RequestType.uservariable)]
public class UserVariableView : IIdView
{
    [Searchable]
    public long id {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.AutoUserId)] 
    public long userId {get;set;}

    [Searchable]
    [WriteRule(WriteRuleType.AutoDate)] 
    public DateTime createDate { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    public DateTime? editDate { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.DefaultValue, WriteRuleType.Increment)]
    public long editCount { get; set; }

    [Searchable]
    [WriteRule(WriteRuleType.None)] //Preserve is automatically set for update btw
    public string key {get;set;} = "";

    [Multiline]
    public string value {get;set;} = "";
}