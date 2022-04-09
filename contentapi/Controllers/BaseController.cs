using System.Runtime.ExceptionServices;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class BaseControllerServices
{
    public ILogger<BaseController> logger;
    public IMapper mapper;
    public IAuthTokenService<long> authService;
    public IEventTracker tracker;
    public RateLimitConfig rateConfig;

    public IDbWriter writer;
    public IGenericSearch searcher;

    public BaseControllerServices(ILogger<BaseController> logger, IAuthTokenService<long> authService, 
        IMapper mapper, IEventTracker tracker, RateLimitConfig rateConfig, IDbWriter writer,
        IGenericSearch search)
    {
        this.logger = logger;
        this.authService = authService;
        this.mapper = mapper;
        this.tracker = tracker;
        this.rateConfig = rateConfig;
        this.writer = writer;
        this.searcher = search;
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

    protected async Task<UserView> GetUserViewStrictAsync()
    {
        var userId = GetUserIdStrict();
        return await services.searcher.GetById<UserView>(RequestType.user, userId) ?? throw new RequestException($"Couldn't find user with id {userId}");
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

            //CAN'T just rethrow, because the middleware strips our CORS junk. Need to return a real error
            return new ObjectResult($"Unhandled exception: {ex}") { StatusCode = 500 };
        }
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