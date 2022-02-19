using contentapi.Search;

namespace contentapi.Views;

//[FromTable(typeof(Db.UserVariable))]
//[ForRequest(RequestType.uservariable)]
[ResultFor(RequestType.uservariable)]
[SelectFrom("user_variables")]
public class UserVariableView : IIdView
{
    //[Searchable]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Preserve)]
    public long id {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoUserId)] 
    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long userId {get;set;}

    //[Searchable]
    //[WriteRule(WriteRuleType.AutoDate)] 
    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.AutoDate)]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.AutoDate)]
    public DateTime? editDate { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.DefaultValue, WriteRuleType.Increment)]
    [FieldSelect]
    [Writable(WriteRule.Preserve, WriteRule.Increment)]
    public long editCount { get; set; }

    //[Searchable]
    //[WriteRule(WriteRuleType.None)] //Preserve is automatically set for update btw
    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public string key {get;set;} = "";

    [Multiline]
    [FieldSelect]
    [Writable]
    public string value {get;set;} = "";
}