namespace contentapi.Main;

public class UploadFileConfig
{
    public string? name { get; set; } = null;
    public string? hash {get;set; } = null;
    public bool tryResize { get; set; } = true;
    public int quantize { get; set; } = -1;
    public string? globalPerms { get; set; } = null;
    public Dictionary<string, string> values { get; set; } = new Dictionary<string, string>();
}