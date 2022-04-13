using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.ban)]
[SelectFrom("bans")]
[WriteAs(typeof(Db.Ban))]
public class BanView : IIdView
{
    [FieldSelect]
    public long id { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long bannedUserId { get; set; }

    [FieldSelect]
    [Writable]
    public DateTime expireDate { get; set; }

    [FieldSelect]
    [Writable]
    public string? message { get; set; } //message is nullable!

    [FieldSelect]
    [Writable]
    public BanType type { get; set; }
}
