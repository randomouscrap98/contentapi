using contentapi.Search;
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
        services.AddSingleton<IRuntimeInformation>(new MyRuntimeInformation(DateTime.Now));
        services.AddSingleton<ITypeInfoService, CachedTypeInfoService>();
        services.AddTransient<IGenericSearch, GenericSearcher>();
        services.AddSingleton<IAuthTokenService<long>, JwtAuthTokenService<long>>();
        services.AddSingleton<ISearchQueryParser, SearchQueryParser>();

        //Configs
        services.AddSingleton<GenericSearcherConfig>();
        services.AddSingleton<JwtAuthTokenServiceConfig>();
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