namespace blog_generator.Configs;

public class TemplateConfig
{
    /// <summary>
    /// The direct path to templates
    /// </summary>
    /// <value></value>
    public string TemplatesFolder {get;set;} = "";

    public string TemplatesExtension {get;set;} = "";

    public List<string> ScriptIncludes {get;set;} = new List<string>();
    public List<string> StyleIncludes {get;set;} = new List<string>();
}