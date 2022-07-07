//using AutoMapper;
//using contentapi.data;
//using contentapi.Db;
//using contentapi.History;
//using contentapi.Live;
//using contentapi.Search;
//using contentapi.Utilities;
//
//namespace contentapi.Main;
//
//public class ControllerDbWriter : DbWriter, IDbWriter
//{
//    public const string RateWrite = "write";
//    public const string RateLogin = "login";
//    public const string RateInteract = "interact";
//    public const string RateFile = "file";
//    public const string RateModule = "module";
//    public const string RateUserVariable = "uservariable";
//
//    protected RateLimitConfig rateConfig;
//
//    public ControllerDbWriter(
//            ILogger<DbWriter> logger, IGenericSearch searcher, ContentApiDbConnection connection, 
//            IViewTypeInfoService typeInfoService, IMapper mapper, IHistoryConverter historyConverter, 
//            IPermissionService permissionService, ILiveEventQueue eventQueue, DbWriterConfig config, 
//            IRandomGenerator rng, IUserService userService, RateLimitConfig rateConfig) : 
//        base(logger, searcher, connection, typeInfoService, mapper, historyConverter, permissionService, eventQueue, config, rng, userService)
//    {
//        this.rateConfig = rateConfig;
//    }
//
//    protected void RateLimit(string thing, string? id = null)
//    {
//        id = id ?? GetUserId()?.ToString() ?? "0";
//        var key = $"{thing}_{id}";
//
//        if(!rateConfig.ParsedRates.ContainsKey(thing))
//            throw new InvalidOperationException($"Missing rate limit configuration for {thing}");
//
//        var limit = rateConfig.ParsedRates[thing];
//        tracker.AddEvent(key);
//
//        var count = tracker.CountEvents(key, limit.Item2);
//
//        if(count > limit.Item1)
//            throw new RateLimitException($"{limit.Item1} requests per {limit.Item2.TotalSeconds} seconds, you're at {count}");
//    }
//}