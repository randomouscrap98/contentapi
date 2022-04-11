namespace contentapi.Main;

public class UploadFileConfigExtra : UploadFileConfig
{
    //These are some auto-generation things so you don't have to create a content view
    public string? name { get; set; } = null;
    public string? hash {get;set; } = null;
    public string? globalPerms { get; set; } = null;
    public Dictionary<string, string> values { get; set; } = new Dictionary<string, string>();
}