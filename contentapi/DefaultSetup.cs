using contentapi.Main;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IAuthTokenService<long>, JwtAuthTokenService<long>>();
        services.AddSingleton<ISearchQueryParser, SearchQueryParser>();
        services.AddSingleton<ICacheCheckpointTracker, CacheCheckpointTracker>();
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IUserService, UserService>();

        //This NEEDS to stay transient because it holds onto a DB connection! We want those recycled!
        services.AddTransient<IGenericSearch, GenericSearcher>();
        services.AddTransient<IDbWriter, DbWriter>();

        //Configs (these have default values given in configs)
        services.AddSingleton<GenericSearcherConfig>();
        services.AddSingleton<JwtAuthTokenServiceConfig>();
        services.AddSingleton<HashServiceConfig>();
        services.AddSingleton<UserServiceConfig>();
        services.AddSingleton<CacheCheckpointTrackerConfig>();
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