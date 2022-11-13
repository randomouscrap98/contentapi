using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;

namespace contentapi.Controllers;

public class BaseControllerServices
{
    public ILogger<BaseController> logger;
    public IMapper mapper;
    public IAuthTokenService<long> authService;
    public IEventTracker tracker;
    public RateLimitConfig rateConfig;

    public IDbServicesFactory dbFactory;

    public BaseControllerServices(ILogger<BaseController> logger, IAuthTokenService<long> authService, 
        IMapper mapper, IEventTracker tracker, RateLimitConfig rateConfig, IDbServicesFactory factory)
    {
        this.logger = logger;
        this.authService = authService;
        this.mapper = mapper;
        this.tracker = tracker;
        this.rateConfig = rateConfig;
        this.dbFactory = factory;
    }
}

public class RateLimitConfig
{
    public Dictionary<string, string> Rates {get;set;} = new Dictionary<string, string>();
    private Dictionary<string, Tuple<int, TimeSpan>>? _parsedRates = null;
    public Dictionary<string, Tuple<int, TimeSpan>> ParsedRates { get {
        if(_parsedRates == null)
        {
            _parsedRates = new Dictionary<string, Tuple<int, TimeSpan>>();

            foreach(var r in Rates)
            {
                var split = r.Value.Split(",".ToCharArray());
                _parsedRates.Add(r.Key, Tuple.Create(int.Parse(split[0]), TimeSpan.FromSeconds(int.Parse(split[1]))));
            }
        }
        return _parsedRates!;
    }}
}

[ApiController]
[Route("api/[controller]")]
public class BaseController : Controller
{
    protected BaseControllerServices services;

    public BaseController(BaseControllerServices services)
    {
        this.services = services;
    }

    public const string RateWrite = "write";
    public const string RateLogin = "login";
    public const string RateInteract = "interact";
    public const string RateFile = "file";
    public const string RateModule = "module";
    public const string RateUserVariable = "uservariable";

    protected long? GetUserId() => services.authService.GetUserId(User.Claims);
    protected bool IsUserLoggedIn() => GetUserId() != null;
    protected long GetUserIdStrict() => services.authService.GetUserId(User.Claims) ?? throw new InvalidOperationException("User not logged in! Strict mode on: MUST be logged in for this call!");

    private IGenericSearch? _cachedSearch = null;
    private IDbWriter? _cachedWriter = null;

    /// <summary>
    /// WARN: ONLY USE THIS IF YOU ARE A SHORT LIVED CONTROLLER!
    /// </summary>
    /// <value></value>
    protected IGenericSearch CachedSearcher { get 
    {
        if(_cachedSearch == null) {
            lock(services) { _cachedSearch = services.dbFactory.CreateSearch(); }
        }

        return _cachedSearch;
    }}

    /// <summary>
    /// WARN: ONLY USE THIS IF YOU ARE A SHORT LIVED CONTROLLER!
    /// </summary>
    /// <value></value>
    protected IDbWriter CachedWriter { get 
    {
        if(_cachedWriter == null) {
            lock(services) { _cachedWriter = services.dbFactory.CreateWriter(); }
        }

        return _cachedWriter;
    }}

    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            lock(services)
            {
                if (_cachedSearch != null)
                {
                    _cachedSearch.Dispose();
                    _cachedSearch = null;
                }
                if (_cachedWriter != null)
                {
                    _cachedWriter.Dispose();
                    _cachedWriter= null;
                }
            }
        }
        base.Dispose(disposing);
    }

    protected async Task<UserView> GetUserViewStrictAsync()
    {
        var userId = GetUserIdStrict();
        return await CachedSearcher.GetById<UserView>(RequestType.user, userId) ?? throw new RequestException($"Couldn't find user with id {userId}");
    }

    protected async Task<ActionResult<T>> MatchExceptions<T>(Func<Task<T>> perform)
    {
        try
        {
            return await perform();
        }
        catch(Exception ex)
        {
            if (ex is AggregateException)
                ex = ex.InnerException ?? throw new InvalidOperationException("Aggregate exception did not have inner exception!", ex); //Grab the first inner exception

            if (ex is ArgumentException || ex is RequestException || ex is ParseException)
                return BadRequest($"Request error: {ex.Message}");

            if (ex is NotFoundException)
                return NotFound($"Not found: {ex.Message}");

            if (ex is ForbiddenException)
                return new ObjectResult($"Forbidden error: {ex.Message}") { StatusCode = 403 }; //Forbidden

            if (ex is RateLimitException)
                return new ObjectResult($"Rate limited: {ex.Message}") { StatusCode = 429 };
            
            if (ex is BannedException)
                return new ObjectResult($"Banned: {ex.Message}") { StatusCode = 418 };
            
            if (ex is TokenException)
                return new ObjectResult($"Session/token exception: {ex.Message}") { StatusCode = 401 };

            //CAN'T just rethrow, because the middleware strips our CORS junk. Need to return a real error
            return new ObjectResult($"Unhandled exception: {ex}") { StatusCode = 500 };
        }
    }

    /// <summary>
    /// A wrapper for writing items with additional protections required for the front-facing API
    /// </summary>
    /// <typeparam name="T"></typeparam>
    protected Task<T> WriteAsync<T>(T item, long? userId = null, string? activityMessage = null) where T : class, IIdView, new()
    {
        var realUserId = userId ?? GetUserIdStrict();
        var limiter = RateWrite;

        if(item is MessageView)
        {
            var message = (item as MessageView)!;

            if(message.module != null)
                throw new ForbiddenException("You cannot create module messages yourself!");

            if(message.receiveUserId != 0)
                throw new ForbiddenException("Setting receiveUserId in a comment is not supported right now!");
        }
        else if(item is ContentView)
        {
            var page = (item as ContentView)!;

            //THIS IS AWFUL! WHAT TO DO ABOUT THIS??? Or is it fine: files ARE written by the controllers after all...
            //so maybe it makes sense for the controllers to control this aspect as well
            if(page.id == 0 && page.contentType == InternalContentType.file)
                throw new ForbiddenException("You cannot create files through this endpoint! Use the file controller!");
        }
        else if(item is WatchView || item is ContentEngagementView || item is MessageEngagementView)
        {
            limiter = RateInteract;
        }
        else if(item is UserVariableView)
        {
            limiter = RateUserVariable;
        }

        RateLimit(limiter, realUserId.ToString());
        using var writer = services.dbFactory.CreateWriter();
        return writer.WriteAsync(item, realUserId, activityMessage);
    }

    protected void RateLimit(string thing, string? id = null)
    {
        id = id ?? GetUserId()?.ToString() ?? "0";
        var key = $"{thing}_{id}";

        if(!services.rateConfig.ParsedRates.ContainsKey(thing))
            throw new InvalidOperationException($"Missing rate limit configuration for {thing}");

        var limit = services.rateConfig.ParsedRates[thing];
        services.tracker.AddEvent(key);

        var count = services.tracker.CountEvents(key, limit.Item2);

        if(count > limit.Item1)
            throw new RateLimitException($"{limit.Item1} requests per {limit.Item2.TotalSeconds} seconds, you're at {count}");
    }
}