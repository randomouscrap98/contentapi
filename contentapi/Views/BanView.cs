using contentapi.Db;
using contentapi.Search;

namespace contentapi.Views;

[ResultFor(RequestType.ban)]
[SelectFrom("bans")]
public class BanView
{
    [FieldSelect]
    public long id { get; set; }

    [FieldSelect]
    public DateTime createDate { get; set; }

    [FieldSelect]
    [Writable]
    public DateTime expireDate { get; set; }

    [FieldSelect]
    public long createUserId { get; set; }

    [FieldSelect]
    [Writable(WriteRule.User, WriteRule.Preserve)]
    public long bannedUserId { get; set; }

    [FieldSelect]
    [Writable]
    public string? message { get; set; } //message is nullable!

    [FieldSelect]
    [Writable]
    public BanType type { get; set; }
}
