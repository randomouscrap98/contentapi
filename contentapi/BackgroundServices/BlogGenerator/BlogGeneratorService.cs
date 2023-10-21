
using System.Text.RegularExpressions;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Main;
using contentapi.Search;

namespace contentapi.BackgroundServices;

public class BlogGeneratorConfig
{
    public TimeSpan Interval {get;set;} = TimeSpan.FromMinutes(5);
    public string TempLocation {get;set;} = "tempfiles";
    public string BlogsFolder {get;set;} = "blogs";
    public string StaticFilesBase {get;set;} = "wwwroot";

    //These are based off the static files base and should be the URL. Path separator will
    //be replaced with system-appropriate for copying
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

public class BlogGeneratorService : BackgroundService
{
    protected ILogger logger;
    protected BlogGeneratorConfig config;
    protected IDbServicesFactory dbfactory;

    public const string SHAREVALUEKEY = "share";
    public const string RESOURCETYPE = "resource";
    public const string STYLESVALUEKEY = "share_styles";
    public const string INDEXFILE = "index.html";

    public const string MAINTEMPLATE = "bloggen_main";
    public const string STYLETEMPLATE = "bloggen_style";


    //public const string ShortIsoFormatString = "yyyy-MM-ddTHH:mm:ssZ";
    //public static string? ShortIsoFormat(DateTime? date) => date?.ToUniversalTime().ToString(ShortIsoFormatString);


    public BlogGeneratorService(ILogger<BlogGeneratorService> logger, BlogGeneratorConfig config, IDbServicesFactory dbfactory)
    {
        this.logger = logger;
        this.config = config;
        this.dbfactory = dbfactory;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(config.Interval.Ticks == 0)
        {
            logger.LogInformation("Blog generation interval 0, disabling blog generation");
            return Task.CompletedTask;
        }

        return ServiceLoop(stoppingToken);
    }

    public async Task ServiceLoop(CancellationToken token)
    {
        var lastRun = DateTime.Now;

        while(!token.IsCancellationRequested)
        {
            try
            {
                var interval = config.Interval - (DateTime.Now - lastRun);

                logger.LogDebug($"Interval to next crawl: {interval}");

                if (interval.Ticks > 0)
                    await Task.Delay(interval, token);
                
                await GenerateBlogs(token);
            }
            catch(OperationCanceledException) { /* These are expected! */ }
            catch(Exception ex)
            {
                logger.LogError($"ERROR DURING BLOG GENERATION: {ex}");
            }
            finally
            {
                lastRun = DateTime.Now;
            }
        }
    }

    public string BlogPath(string hash) => Path.Join(config.BlogsFolder, hash);
    public string BlogPath(ContentView view) => Path.Join(config.BlogsFolder, view.hash);

    public long GetBlogRevision(ContentView blog, List<ContentView> children)
    {
        var maxId = blog.lastRevisionId;

        foreach(var c in children)
            if(c.lastRevisionId > maxId)
                maxId = c.lastRevisionId;

        return maxId;
    }

    /// <summary>
    /// Given the base content and all related content, figure out if a blog should be regenerated
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public async Task<bool> BlogRequiresRegeneration(ContentView blog, List<ContentView> children)
    {
        var indexPath = Path.Join(BlogPath(blog), INDEXFILE);

        if(File.Exists(indexPath))
        {
            //Compute the max revision and blog requires regeneration if the revision in the index
            //is not the same.
            var maxRevision = GetBlogRevision(blog, children);
            var lines = await File.ReadAllLinesAsync(indexPath);
            return !Regex.IsMatch(lines[2], @$"^<!--{maxRevision}-->");
        }

        return true;
    }

    public async Task GenerateBlogs(CancellationToken token)
    {
        using var searcher = dbfactory.CreateSearch();

        //We assume most blogs won't need regeneration, so we request a couple small subsets first just to see.
        //First set are the blog parents themselves, then we go into each one to determine which need regeneration
        var baseBlogs = await searcher.SearchSingleTypeUnrestricted<ContentView>(new data.SearchRequest() {
            type = nameof(RequestType.content),
            fields = "id,hash,values,contentType",
            query = "!valuelike(@key, @value) and contentType=@type"
        }, new Dictionary<string, object> {
            { "key", SHAREVALUEKEY },
            { "value", "true" },
            { "type", InternalContentType.page },
        });

        logger.LogDebug($"Checking for updates on {baseBlogs.Count} blogs: {string.Join(",", baseBlogs.Select(x => x.hash))}");

        foreach(var blog in baseBlogs)
        {
            //Now go get another subset of the data: just enough to compute whether this blog needs regeneration.
            var blogContents = await searcher.SearchSingleTypeUnrestricted<ContentView>(new data.SearchRequest() {
                type = nameof(RequestType.content),
                fields = "id,hash,literalType,contentType,lastRevisionId",
                query = "id=@id or (parentId=@id and literalType=@resource) or hash in @stylehashes",
            }, new Dictionary<string, object> {
                { "id", blog.id },
                { "resource", RESOURCETYPE },
                { "stylehashes", blog.values[STYLESVALUEKEY] },
            });

            if(await BlogRequiresRegeneration(blog, blogContents))
            {
                logger.LogInformation($"Regenerating blog {blog.hash}");

                //We first lookup the information we left off, then we can regenerate with the full info
                var fullChildren = await searcher.SearchSingleTypeUnrestricted<ContentView>(new data.SearchRequest() {
                    type = nameof(RequestType.content),
                    fields = "*",
                    query = "id in @ids"
                }, new Dictionary<string, object> {
                    { "ids", blogContents.Select(x => x.id) }
                });

                await RegenerateBlog(await searcher.GetById<ContentView>(blog.id), fullChildren);
            }
            else
            {
                logger.LogDebug($"Blog {blog.hash} up to date, skipping");
            }
        }
    }

    /// <summary>
    /// Perform a full blog regeneration. Does NOT remove existing blog, to minimize downtime!
    /// </summary>
    /// <param name="blog"></param>
    /// <param name="children"></param>
    /// <returns></returns>
    public async Task RegenerateBlog(ContentView blog, List<ContentView> children)
    {
        var basePath = BlogPath(blog);

        if(!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);
        
        //Copy all the static files in to this folder
        foreach(var file in config.ScriptIncludes.Union(config.StyleIncludes))
        {
            var realPath = file.Replace('/', Path.DirectorySeparatorChar);
            if(realPath.Contains(Path.DirectorySeparatorChar))
                Directory.CreateDirectory(Path.GetDirectoryName(realPath)!);
            File.Copy(Path.Join(config.StaticFilesBase, realPath), Path.Join(basePath, realPath), true);
        }
    }
}