namespace blog_generator.Configs;

public class TemplateConfig
{
    /// <summary>
    /// The direct path to templates
    /// </summary>
    /// <value></value>
    public string TemplatesFolder {get;set;} = "Templates";

    public string TemplatesExtension {get;set;} = ".mustache";

    public string MainTemplate {get;set;} = "bloggen_main";
    public string StyleTemplate {get;set;} = "bloggen_style";

    public List<string> ScriptIncludes {get;set;} = new List<string>() {
      "markup/parse.js", 
      "markup/render.js", 
      "markup/langs.js", 
      "markup/legacy.js", 
      "markup/helpers.js", 
      "bloggen.js" 
    };

    public List<string> StyleIncludes {get;set;} = new List<string>() {
      "markup/markup.css", 
      "bloggen.css" 
    };
}
