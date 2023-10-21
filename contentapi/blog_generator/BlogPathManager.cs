using blog_generator.Configs;

namespace blog_generator;

public class BlogPathManager
{
    protected PathManagementConfig config;

    public BlogPathManager(PathManagementConfig config)
    {
        this.config = config;
    }

    protected string StylesFolder => Path.Join(config.LocalContentRoot, config.StylesFolder);
    protected string BlogFolder => Path.Join(config.LocalContentRoot, config.BlogFolder);

    public string LocalStylePath(string hash) => Path.Join(StylesFolder, $"{hash}.css");
    public string LocalBlogMainPath(string hash) => Path.Join(BlogFolder, hash, "index.html");
    public string LocalBlogPagePath(string hash, string pageHash) => Path.Join(BlogFolder, hash, $"{pageHash}.html");

    public string WebStylePath(string hash) => WebContentpath($"{config.StylesFolder}/{hash}.css");
    public string WebBlogMainPath(string hash) => WebContentpath($"{config.BlogFolder}/{hash}/");
    public string WebBlogPagePath(string hash, string pageHash) => WebContentpath($"{config.BlogFolder}/{hash}/{pageHash}.html");

    public string WebContentpath(string resource) => $"{config.WebContentRoot}{resource.TrimStart('/')}";
    public string WebStaticPath(string resource) => $"{config.WebStaticRoot}{resource.TrimStart('/')}";

    public bool LocalStyleExists(string hash) => File.Exists(LocalStylePath(hash));
    public bool LocalBlogMainExists(string hash) => File.Exists(LocalBlogMainPath(hash));

    //public string ImagePath(string hash) => $"{config.ImageRoot}/hash";

    public List<string> GetAllBlogHashes()
    {
        if(!Directory.Exists(BlogFolder))
            return new List<string>();

        return Directory.EnumerateDirectories(BlogFolder).Select(x => Path.GetFileName(x)).ToList();
    }

    public List<string> GetAllStyleHashes()
    {
        if(!Directory.Exists(StylesFolder))
            return new List<string>();

        return Directory.EnumerateFiles(StylesFolder).Select(x => Path.GetFileNameWithoutExtension(x)).ToList();
    }
}