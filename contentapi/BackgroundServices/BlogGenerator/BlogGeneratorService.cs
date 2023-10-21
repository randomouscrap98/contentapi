
using System.Text.RegularExpressions;
using blog_generator;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Db;
using contentapi.Main;
using contentapi.Search;

namespace contentapi.BackgroundServices;

public class BlogGeneratorConfig
{
    public TimeSpan Interval {get;set;} = TimeSpan.FromMinutes(0);  //Disable by default
    //public string TempLocation {get;set;} = "tempfiles";
    public string BlogsFolder {get;set;} = "blogs";         //Might as well have SOME folder so it's not writing in root
    public string StaticFilesBase {get;set;} = "wwwroot";   //This should basically be a constant

    //These are based off the static files base and should be the URL. Path separator will
    //be replaced with system-appropriate for copying
    public List<string> ScriptIncludes {get;set;} = new List<string>() { };
    public List<string> StyleIncludes {get;set;} = new List<string>() { };
}

public class BlogGeneratorService : BackgroundService
{
    protected ILogger logger;
    protected BlogGeneratorConfig config;
    protected IDbServicesFactory dbfactory;
    protected TemplateLoader templateLoader;

    public const string SHAREVALUEKEY = "share";
    public const string RESOURCETYPE = "resource";
    public const string STYLESVALUEKEY = "share_styles";
    public const string INDEXFILE = "index.html";
    public const string REVISIONFILE = ".revision";

    public const string MAINTEMPLATE = "bloggen_main";
    public const string STYLETEMPLATE = "bloggen_style";

    public const long TicksPerSecond = 10000000;


    public BlogGeneratorService(ILogger<BlogGeneratorService> logger, BlogGeneratorConfig config, IDbServicesFactory dbfactory,
        TemplateLoader templateLoader)
    {
        this.logger = logger;
        this.config = config;
        this.dbfactory = dbfactory;
        this.templateLoader = templateLoader;
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
        var revPath = Path.Join(BlogPath(blog), REVISIONFILE);

        if(File.Exists(revPath))
        {
            //Compute the max revision and blog requires regeneration if the revision in the index
            //is not the same.
            var maxRevision = GetBlogRevision(blog, children);
            var text = await File.ReadAllTextAsync(revPath);
            return !Regex.IsMatch(text, @$"{maxRevision}");
        }

        return true;
    }

    /// <summary>
    /// Find and loop over blogs for generation (or not, maybe they're up-to-date)
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
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
                fields = "id,hash,literalType,contentType,parentId,lastRevisionId",
                query = "id=@id or (parentId=@id and literalType=@resource) or hash in @stylehashes",
            }, new Dictionary<string, object> {
                { "id", blog.id },
                { "resource", RESOURCETYPE },
                { "stylehashes", blog.values.GetValueOrDefault(STYLESVALUEKEY, new List<string>()) },
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

                //Also, apparently we need activity for each and a whole bunch of nonsense
                var activity = await searcher.SearchSingleTypeUnrestricted<ActivityView>(new data.SearchRequest() {
                    type = nameof(RequestType.activity),
                    fields = "*",
                    query = "id in @ids or id = @baseid"
                }, new Dictionary<string, object> {
                    { "ids", blogContents.Select(x => x.lastRevisionId) },
                    { "baseid", blog.lastRevisionId }
                });

                var userIds = fullChildren.Select(x => x.createUserId).Union(activity.Select(x => x.userId));
                userIds.Append(blog.createUserId);

                //AND USERS?? This is getting out of hand!
                var users = await searcher.SearchSingleTypeUnrestricted<UserView>(new data.SearchRequest() {
                    type = nameof(RequestType.user),
                    fields = "*",
                    query = "id in @ids"
                }, new Dictionary<string, object> {
                    { "ids", userIds.Distinct() }
                });

                await RegenerateBlog(await searcher.GetById<ContentView>(blog.id), fullChildren, activity, users);
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
    public async Task RegenerateBlog(ContentView blog, List<ContentView> children, List<ActivityView> activity, List<UserView> users)
    {
        var basePath = BlogPath(blog);

        if(!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);
        
        //Copy all the static files in to this folder
        foreach(var file in config.ScriptIncludes.Union(config.StyleIncludes))
        {
            var localPath = file.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.Join(basePath, localPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(Path.Join(config.StaticFilesBase, localPath), destinationPath, true);
        }

        //await RegenerateBlogPost(blog, blog, activity, users);

        // Only at the END do you write the revision file
        var revisionId = GetBlogRevision(blog, children);
        var revPath = Path.Join(BlogPath(blog), REVISIONFILE);
        await File.WriteAllTextAsync(revPath, @$"{revisionId}");
    }

    public string GetAuthorFromList(long id, IEnumerable<UserView> users) => users.FirstOrDefault(x => x.id == id)?.username ?? "???";

    //public async Task RegenerateBlogPost(ContentView page, ContentView parent, List<ActivityView> activity, List<UserView> users)
    //{
    //    var revision = activity.FirstOrDefault(x => x.id == page.lastRevisionId);

    //    //This generates a single blogpost. It figures out how to generate it based on the data given. If the page itself IS the parent,
    //    //something else MAY be done.
    //    var templateData = new MainTemplateData()
    //    {
    //        scripts = config.ScriptIncludes,
    //        styles = config.StyleIncludes,
    //        page = page,
    //        parent = parent,
    //        render_date = DateTime.UtcNow,
    //        version = (DateTime.Now.Ticks / TicksPerSecond).ToString(),
    //        keywords = string.Join(", ", page.keywords.Union(parent.keywords)),
    //        parent_link = pathManager.WebBlogMainPath(parent.hash),
    //        author = GetAuthorFromList(page.createUserId, users),
    //        edit_author = GetAuthorFromList(revision?.userId ?? -1, users),
    //        revision = revision
    //    };

    //    templateData.styles.AddRange(GetStylesForParent(parent).Select(x => pathManager.WebStylePath(x)));

    //    templateData.navlinks = pages.OrderByDescending(x => x.createDate).Select(x => new NavigationItem()
    //    {
    //        text = x.name,
    //        link = pathManager.WebBlogPagePath(parent.hash, x.hash),
    //        current = x.id == page.id,
    //        create_date = x.createDate,
    //        hash = x.hash
    //    }).ToList();

    //    //Need to use mustache here to generate the template and write it
    //    var renderedPage = await templateLoader.RenderPageAsync(MAINTEMPLATE, templateData);

    //    var path = Path.Combine(basePath, page.id == parent.id ? INDEXFILE : pathManager.LocalBlogPagePath(parent.hash, page.hash);
    //    await WriteAny(path, renderedPage, "page");
    //}
}