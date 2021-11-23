using contentapi.Implementations;
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
    }

    /// <summary>
    /// An easy (default) way to add configs in as "intuitive" of a way as possible. Configs added through this endpoint are SINGLETONS
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static void AddConfigBinding<T>(IServiceCollection services, IConfiguration config) where T : class
    {
        var name = typeof(T).Name;
        services.Configure<T>(config.GetSection(name));
        services.AddSingleton<T>(p => (p.GetService<IOptionsMonitor<T>>() ?? throw new InvalidOperationException($"Mega config failure on {name}!")).CurrentValue);
    }
}