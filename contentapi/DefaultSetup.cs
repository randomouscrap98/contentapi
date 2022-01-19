using System.Collections.Concurrent;
using contentapi.Live;
using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace contentapi.Setup;

public static class DefaultSetup
{
    /// <summary>
    /// Add all default service implementations to the given service container. Can override later of course.
    /// </summary>
    /// <remarks>
    /// To replace services (such as for unit tests), you can do: services.Replace(ServiceDescriptor.Transient<IFoo, FooB>());i
    /// </remarks>
    /// <param name="services"></param>
    public static void AddDefaultServices(IServiceCollection services)
    {
        //Since we consume the db, also call their setup here
        Db.Setup.DefaultSetup.AddDefaultServices(services);

        services.AddAutoMapper(typeof(SearchRequestPlusProfile)); //You can pick ANY profile, it just needs some type from this binary

        services.AddSingleton<IRuntimeInformation>(new MyRuntimeInformation(DateTime.Now));
        services.AddSingleton<ITypeInfoService, CachedTypeInfoService>();
        services.AddSingleton<IQueryBuilder, QueryBuilder>();
        services.AddSingleton<ISearchQueryParser, SearchQueryParser>();
        services.AddSingleton(typeof(IAuthTokenService<>), typeof(JwtAuthTokenService<>));
        services.AddSingleton(typeof(ICacheCheckpointTracker<>), typeof(CacheCheckpointTracker<>));
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IPermissionService, PermissionService>();

        //Our singleton cache for the event queue!
        var eventQueueCache = new ConcurrentDictionary<int, AnnotatedCacheItem>();

        //This NEEDS to stay transient because it holds onto a DB connection! We want those recycled!
        services.AddTransient<IGenericSearch, GenericSearcher>();
        services.AddTransient<IDbWriter, DbWriter>();
        services.AddTransient<IEventQueue>(p =>
        {
            return new EventQueue(
                p.GetService<ILogger<EventQueue>>() ?? throw new InvalidOperationException("Can't make ILogger for event queue!"), 
                p.GetService<ICacheCheckpointTracker<EventData>>() ?? throw new InvalidOperationException("Can't make ICacheCheckpointTracker for event queue!"),
                p.GetService<IGenericSearch>() ?? throw new InvalidOperationException("Can't make IGenericSearch for event queue!"),
                p.GetService<IPermissionService>() ?? throw new InvalidOperationException("Can't make IPermissionService for event queue!"),
                eventQueueCache
            );
        });

        //Configs (these have default values given in configs)
        services.AddSingleton<GenericSearcherConfig>();
        services.AddSingleton<JwtAuthTokenServiceConfig>();
        services.AddSingleton<HashServiceConfig>();
        services.AddSingleton<UserServiceConfig>();
        services.AddSingleton<CacheCheckpointTrackerConfig>();
    }

    public static TokenValidationParameters AddSecurity(IServiceCollection services, string secretKey)
    {
        //Not sure if this is ok, but adding security stuff to the service collection.
        //It's just me using it, and I need this stuff in multiple places
        var validationParameters = new TokenValidationParameters()
        {
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
        services.AddSingleton(validationParameters);
        services.AddSingleton(
            new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(secretKey)),
                SecurityAlgorithms.HmacSha256Signature)
        );
        return validationParameters;
    }

    /// <summary>
    /// An easy (default) way to add configs in as "intuitive" of a way as possible. Configs added through this endpoint are SINGLETONS
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void AddConfigBinding<T>(IServiceCollection services, IConfiguration config) where T : class
    {
        var ct = typeof(T);
        var name = ct.Name;
        services.Configure<T>(config.GetSection(name));
        var generator = new Func<IServiceProvider, T>(p => (p.GetService<IOptionsMonitor<T>>() ?? throw new InvalidOperationException($"Mega config failure on {name}!")).CurrentValue);

        //If it already exists (maybe with default values), replace it. They clearly 
        //actually want it from the config
        if(services.Any(x => x.ServiceType == ct))
            services.Replace(ServiceDescriptor.Singleton<T>(generator));
        else
            services.AddSingleton<T>(generator);
    }
}