using contentapi.data;

namespace contentapi.data.Views;

[ResultFor(RequestType.userrelation)]
[SelectFrom("user_relations")]
[WriteAs(typeof(Db.UserRelation))]
public class UserRelationView : IIdView
{
    [DbField]
    public long id { get; set; }

    [DbField]
    [Writable]
    public UserRelationType type { get; set; }

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable] //Although these are writable, only supers should be able to write these!
    public long userId { get; set; }

    [DbField]
    [Writable]
    public long relatedId { get; set; }
}