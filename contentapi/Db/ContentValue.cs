using Dapper.Contrib.Extensions;

namespace contentapi.Db;

[Table("content_values")]
public class ContentValue
{
    [Key]
    public long id { get; set; }
    public long contentId { get; set; }
    public string key { get; set; } = "";
    public string value { get; set; } = "";
}