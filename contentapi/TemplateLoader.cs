using Stubble.Core.Builders;
using Stubble.Core.Interfaces;

namespace contentapi;

//TODO: put this back in appsettings!
public class TemplateConfig
{
    public string TemplatesFolder {get;set;} = "Templates";
    public string TemplatesExtension {get;set;} = ".mustache";
}

/// <summary>
/// A partials (templates) loader for Mustache (called Stubble) which loads partials from files
/// by name. This was the default behavior in standard Mustache, Stubble decided they were too
/// good to provide the default behavior to anybody.
/// </summary>
public class TemplateLoader : IStubbleLoader
{
    /// <summary>
    /// The folder to get the partials from
    /// </summary>
    protected TemplateConfig config;

    public TemplateLoader(TemplateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// An IStubbleLoader required function, clones ourselves
    /// </summary>
    /// <returns>A shallow copy of "this"</returns>
    public IStubbleLoader Clone()
    {
        return (IStubbleLoader)this.MemberwiseClone();
    }

    /// <summary>
    /// Load a partial (a template) by name; required by IStubbleLoader
    /// </summary>
    /// <param name="name">The name of the partial to load (not a full path, just a name)</param>
    /// <returns>The partial contents</returns>
    public string Load(string name)
    {
        return LoadAsync(name).Result;
    }

    public string TemplatePath(string name)
    {
        return Path.Combine(config.TemplatesFolder, name + config.TemplatesExtension);
    }

    /// <summary>
    /// Load a partial (a template) by name asynchronously; required by IStubbleLoader
    /// </summary>
    /// <param name="name">The name of the partial to load (not a full path, just a name)</param>
    /// <returns>The partial contents</returns>
    public async ValueTask<string> LoadAsync(string name)
    {
        var path = TemplatePath(name);

        if (!File.Exists(path)) 
            throw new InvalidOperationException($"Cannot find partial {path}");

        return await File.ReadAllTextAsync(path);
    }

    /// <summary>
    /// I hate this library, I don't care if it's stupid. The partials loader is required to get the renderer to function
    /// correctly, but why not just make it both? Thus, the partials loader is also the template loader... as it probably should be
    /// </summary>
    /// <param name="page"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public async Task<string> RenderPageAsync(string page, object data) //Dictionary<string, object?> data)
    {
        var pageRaw = await File.ReadAllTextAsync(TemplatePath(page));

        var stubble = new StubbleBuilder().Configure(s => {
            s.AddToPartialTemplateLoader(this);
        }).Build();

        return await stubble.RenderAsync(pageRaw, data);
    }
}