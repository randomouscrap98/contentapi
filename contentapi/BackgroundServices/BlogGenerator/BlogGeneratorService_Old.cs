//using System.Net.WebSockets;
//using AutoMapper;
//using blog_generator;
//using blog_generator.Configs;
//using contentapi.data;
//using contentapi.data.Views;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//
//namespace contentapi.BackgroundServices;
//
//public class BlogGeneratorService_Old : BackgroundService
//{
//    private readonly ILogger logger;
//    protected WebsocketConfig wsconfig;
//    protected BlogGenerator blogGenerator;
//    protected BlogPathManager pathManager;
//    protected IMapper mapper;
//
//    public const string contentName = nameof(RequestType.content);
//    public const string userName = nameof(RequestType.user);
//    public const string activityName = nameof(RequestType.activity);
//    public const string blogFields = "id, name, text, hash, lastRevisionId, values, createUserId, createDate, parentId, keywords, description, contentType, literalType";
//    public const string userFields = "id, username, createDate, avatar";
//
//    public const string requestKey = "request";
//    public const string liveKey = "live";
//    public const string shareKey = "share";
//    public const string resourceKey = "resource";
//    //public const string shareIgnoreKey = "share_ignore";
//    public const string parentKey = "parent";
//
//    public const string initialPrecheckKey = "initial_precheck";
//    public const string blogRefreshKey = "blog_refresh";
//    public const string blogParentKey = "blog_parent";
//    public const string blogPagesKey = "blog_pages";
//    public const string stylePagesKey = "style_pages";
//    public const string styleRefreshKey = "style_refresh";
//
//    public BlogGeneratorService_Old(ILogger<BlogGeneratorService_Old> logger, WebsocketConfig wsconfig, BlogGenerator blogGenerator, IMapper mapper,
//        BlogPathManager pathManager)
//    {
//        this.logger = logger;
//        this.wsconfig = wsconfig;
//        this.blogGenerator = blogGenerator;
//        this.mapper = mapper;
//        this.pathManager = pathManager;
//    }
//
//    public SearchRequests GetFullBlogRegenSearchRequest(string hash)
//    {
//        return new SearchRequests()
//        {
//            values = new Dictionary<string, object>() {
//                { "hash", hash },
//                { "type", InternalContentType.page },
//                { "resource", resourceKey }
//            },
//            requests = new List<SearchRequest>() {
//                new SearchRequest() {
//                    name = blogParentKey,
//                    type = contentName,
//                    fields = blogFields,
//                    query = "hash = @hash and contentType = @type"
//                },
//                new SearchRequest() {
//                    name = blogPagesKey,
//                    type = contentName,
//                    fields = blogFields,
//                    query = $"parentId in @{blogParentKey}.id and contentType = @type and literalType = @resource"
//                },
//                new SearchRequest() {
//                    type = activityName,
//                    fields = "*",
//                    query = $"id in @{blogPagesKey}.lastRevisionId or id in @{blogParentKey}.lastRevisionId"
//                },
//                new SearchRequest() {
//                    type = userName,
//                    fields = userFields,
//                    query = $"id in @{blogParentKey}.createUserId or id in @{blogPagesKey}.createUserId or id in @{activityName}.{nameof(ActivityView.userId)}"
//                }
//            }
//        };
//    }
//
//    public SearchRequests GetStyleRegenRequest(List<string> styles)
//    {
//        return new SearchRequests()
//        {
//            values = new Dictionary<string, object>() {
//                { "hashes", styles }
//            },
//            requests = new List<SearchRequest>() {
//                new SearchRequest() {
//                    type = contentName,
//                    fields = blogFields, //Should also be enough for styles...
//                    query = "hash in @hashes"
//                },
//                new SearchRequest() {
//                    type = userName,
//                    fields = userFields,
//                    query = $"id in @{contentName}.createUserId"
//                }
//            }
//        };
//    }
//
//    //Only staging, because we still have to send the new request and get it back...
//    protected async Task BlogStaging(ContentView content, Func<WebSocketRequest, Task> sendFunc, bool force = false)
//    {
//        logger.LogDebug($"Testing blog {content.hash}({content.id}) for regeneration. Apparent current revision: {content.lastRevisionId}, forcing: {force}");
//
//        if(!(content.values.ContainsKey(shareKey) && content.values[shareKey].ToString()?.ToLower() == "true"))
//        {
//            logger.LogInformation($"Page {content.hash}({content.id}) doesn't appear to be a blog, removing it if it exists");
//            blogGenerator.DeleteBlog(content.hash);
//            return;
//        }
//
//        if(force || await blogGenerator.ShouldRegenBlog(content.hash, content.lastRevisionId))
//        {
//            logger.LogInformation($"Requesting recreate of entire blog '{content.hash}'({content.id}) (forced: {force})");
//
//            //This will also refresh (unconditionally?) the style
//            await sendFunc(new WebSocketRequest()
//            {
//                id = blogRefreshKey,
//                type = requestKey,
//                data = GetFullBlogRegenSearchRequest(content.hash)
//            });
//        }
//        else
//        {
//            logger.LogInformation($"Blog {content.hash}({content.id}) up to date, leaving it alone");
//        }
//    }
//
//    public async Task StyleStaging(IEnumerable<ContentView> styles, Func<WebSocketRequest, Task> sendFunc, string onBehalf, bool force = false)
//    {
//        logger.LogDebug($"Testing {styles.Count()} styles  for regeneration for {onBehalf}. forcing: {force}");
//        var regenStyles = await blogGenerator.GetRegenStyles(styles);
//
//        if(!force)
//        {
//            var existingHashes = pathManager.GetAllStyleHashes();
//            regenStyles = regenStyles.Intersect(existingHashes).ToList();
//        }
//
//        if (regenStyles.Count > 0)
//        {
//            logger.LogInformation($"Re-obtaining {regenStyles.Count} styles that were apparently updated in {onBehalf}");
//            await sendFunc(new WebSocketRequest()
//            {
//                id = styleRefreshKey,
//                type = requestKey,
//                data = GetStyleRegenRequest(regenStyles)
//            });
//        }
//        else
//        {
//            logger.LogInformation($"No styles need to be regenerated in {onBehalf}");
//        }
//    }
//
//    protected async Task HandleResponse(WebSocketResponse response, Func<WebSocketRequest, Task> sendFunc)
//    {
//        logger.LogDebug($"Received response type {response.type}, id {response.id}, error '{response.error}'");
//
//        if(response.data == null)
//        {
//            logger.LogInformation($"Null data in response type {response.type} (id: {response.id}), ignoring");
//            return;
//        }
//
//        if(response.id == initialPrecheckKey)
//        {
//            var responseData = ((JObject)response.data).ToObject<GenericSearchResult>() ?? 
//                throw new InvalidOperationException($"Couldn't convert {initialPrecheckKey} response data to GenericSearchResult");
//
//            var contents = blog_generator.Utilities.ForceCastResultObjects<ContentView>(responseData, blogPagesKey, initialPrecheckKey);
//            var styles = blog_generator.Utilities.ForceCastResultObjects<ContentView>(responseData, stylePagesKey, initialPrecheckKey);
//            logger.LogDebug($"Initial_precheck: {contents.Count} potential blogs found, {styles.Count} potential styles");
//
//            //Remove old blogs that are no longer in service, ie blogs on the system that weren't returned in the full check
//            blogGenerator.CleanupMissingBlogs(contents.Select(x => x.hash));
//            
//            await StyleStaging(styles, sendFunc, initialPrecheckKey);
//
//            //Then go regen all the blogs. Yes, this might be a lot of individual lookups but oh well
//            foreach(var blog in contents)
//                await BlogStaging(blog, sendFunc);
//        }
//        else if(response.id == blogRefreshKey)
//        {
//            var responseData = ((JObject)response.data).ToObject<GenericSearchResult>() ?? 
//                throw new InvalidOperationException($"Couldn't convert {blogRefreshKey} response data to GenericSearchResult");
//
//            var users = blog_generator.Utilities.ForceCastResultObjects<UserView>(responseData, userName, blogRefreshKey); 
//            var parent = blog_generator.Utilities.ForceCastResultObjects<ContentView>(responseData, blogParentKey, blogRefreshKey).First();
//            var pages = blog_generator.Utilities.ForceCastResultObjects<ContentView>(responseData, blogPagesKey, blogRefreshKey);
//            var activity = blog_generator.Utilities.ForceCastResultObjects<ActivityView>(responseData, activityName, blogRefreshKey);
//
//            //Need to go get styles here, it won't be part of the blog generation
//            var styles = blogGenerator.GetStylesForParent(parent);
//
//            if(styles.Count > 0)
//            {
//                await sendFunc(new WebSocketRequest()
//                {
//                    id = styleRefreshKey,
//                    type = requestKey,
//                    data = GetStyleRegenRequest(styles)
//                });
//            }
//
//            //And then blog generation
//            await blogGenerator.GenerateFullBlog(parent, pages, users, activity);
//        }
//        else if(response.id == styleRefreshKey)
//        {
//            var responseData = ((JObject)response.data).ToObject<GenericSearchResult>() ?? 
//                throw new InvalidOperationException($"Couldn't convert {styleRefreshKey} response data to GenericSearchResult");
//
//            var contents = blog_generator.Utilities.ForceCastResultObjects<ContentView>(responseData, contentName, styleRefreshKey);
//            var users = blog_generator.Utilities.ForceCastResultObjects<UserView>(responseData, userName, styleRefreshKey);
//
//            //Now just regen the contents as styles
//            foreach(var style in contents)
//            {
//                await blogGenerator.GenerateStyle(style, users);
//            }
//        }
//        else if (response.type == liveKey)
//        {
//            var liveData = ((JObject)response.data).ToObject<LiveData>() ?? 
//                throw new InvalidOperationException($"Couldn't convert {liveKey} response data to LiveData!");
//            
//            if(liveData.objects.ContainsKey(EventType.activity_event))
//            {
//                var realObjects = liveData.objects[EventType.activity_event];
//
//                var contents = blog_generator.Utilities.ForceCastResultObjects<ContentView>(realObjects, contentName, liveKey);
//                var parents = blog_generator.Utilities.ForceCastResultObjects<ContentView>(realObjects, parentKey, liveKey);
//
//                //Don't need to check parents, they're not the ones getting updated
//                await StyleStaging(contents, sendFunc, liveKey);
//
//                //If the direct content to be modified was a blog, do the standard procedure
//                foreach(var content in contents)
//                    await BlogStaging(content, sendFunc);
//                
//                //But, if the thing is a child of a blog, force the regeneration of the whole thing to be safe
//                foreach(var parent in parents)
//                    await BlogStaging(parent, sendFunc, true);
//            }
//        }
//    }
//
//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        while (!stoppingToken.IsCancellationRequested)
//        {
//            try
//            {
//                using var ws = new ClientWebSocket();
//                using var ms = new MemoryStream();
//
//                var sender = new Func<WebSocketRequest, Task>((o) => ws.SendObjectAsync<WebSocketRequest>(o, WebSocketMessageType.Text, stoppingToken));
//                var websocketUrl = $"{wsconfig.WebsocketEndpoint}?token={wsconfig.AnonymousToken}";
//
//                logger.LogInformation($"Connecting to: {websocketUrl}");
//
//                await ws.ConnectAsync(new Uri(websocketUrl), stoppingToken);
//
//                //We HAVE to wait for the initial lastId message
//                var connectResponse = await ws.ReceiveObjectAsync<WebSocketResponse>(ms, stoppingToken);
//
//                logger.LogInformation($"Websocket connection opened, response: {JsonConvert.SerializeObject(connectResponse)}");
//
//                //Don't worry about styles here, they will be brought in as part of the blog regeneration, which this precheck is pulling
//                var precheckRequest = new WebSocketRequest()
//                {
//                    id = initialPrecheckKey,
//                    type = requestKey,
//                    data = new SearchRequests()
//                    {
//                        values = new Dictionary<string, object>() {
//                            { "key", shareKey },
//                            { "value", "true" },
//                            { "type", InternalContentType.page },
//                            { "existing_styles", pathManager.GetAllStyleHashes() }
//                        },
//                        requests = new List<SearchRequest>() {
//                            new SearchRequest() {
//                                name = blogPagesKey,
//                                type = contentName,
//                                fields = "id, lastRevisionId, hash, values, contentType",
//                                query = "!valuelike(@key, @value) and contentType = @type"
//                            },
//                            new SearchRequest() {
//                                name = stylePagesKey,
//                                type = contentName,
//                                fields = "id, lastRevisionId, hash",
//                                query = "hash in @existing_styles"
//                            }
//                        }
//                    }
//                };
//
//                //First, do the rescan
//                logger.LogInformation("Requesting precheck data now...");
//                await sender(precheckRequest);
//
//                logger.LogInformation("Beginning listen loop...");
//
//                //Then, just listen
//                while(!stoppingToken.IsCancellationRequested)
//                {
//                    var listenResponse = await ws.ReceiveObjectAsync<WebSocketResponse>(ms, stoppingToken);
//                    await HandleResponse(listenResponse, sender);
//                }
//            }
//            catch(Exception ex)
//            {
//                logger.LogWarning($"Exception broke out of websocket loop: \n{ex}\nWill retry in {wsconfig.ReconnectInterval}...");
//                await Task.Delay(wsconfig.ReconnectInterval, stoppingToken);
//            }
//        }
//    }
//}
//