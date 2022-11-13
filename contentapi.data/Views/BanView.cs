
namespace contentapi.data.Views;

[ResultFor(RequestType.ban)]
[SelectFrom("bans")]
[WriteAs(typeof(Db.Ban))]
public class BanView : IIdView
{
    [DbField]
    public long id { get; set; }

    [DbField]
    [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
    public DateTime createDate { get; set; }

    [DbField]
    [Writable(WriteRule.AutoUserId, WriteRule.Preserve)]
    public long createUserId { get; set; }

    [DbField]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long bannedUserId { get; set; }

    [DbField]
    [Writable]
    public DateTime expireDate { get; set; }

    [DbField]
    [Writable]
    public string? message { get; set; } //message is nullable!

    [DbField]
    [Writable]
    public BanType type { get; set; }
}
