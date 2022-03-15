using System.Runtime.ExceptionServices;
using contentapi.Live;
using contentapi.Main;
using contentapi.Module;
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

        //For anything that just needs a simple search here and there, but doesn't want to be transient
        services.AddSingleton(p => new Func<IGenericSearch>(() => p.GetService<IGenericSearch>() ?? throw new InvalidOperationException("Couldn't create IGenericSearch somehow!")));

        services.AddSingleton<IRuntimeInformation>(new MyRuntimeInformation(DateTime.Now));
        services.AddSingleton<IViewTypeInfoService, ViewTypeInfoService_Cached>();
        services.AddSingleton<IQueryBuilder, QueryBuilder>();
        services.AddSingleton<ISearchQueryParser, SearchQueryParser>();
        services.AddSingleton(typeof(IAuthTokenService<>), typeof(JwtAuthTokenService<>));
        services.AddSingleton(typeof(ICacheCheckpointTracker<>), typeof(CacheCheckpointTracker<>));
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<ILiveEventQueue, LiveEventQueue>();
        services.AddSingleton<IEventTracker, EventTracker>();
        services.AddSingleton<IUserStatusTracker, UserStatusTracker>();

        //This NEEDS to stay transient because it holds onto a DB connection! We want those recycled!
        services.AddTransient<IGenericSearch, GenericSearcher>();
        services.AddTransient<IDbWriter, DbWriter>();
        services.AddTransient<ShortcutsService>();

        //Configs (these have default values given in configs)
        services.AddSingleton<GenericSearcherConfig>();
        services.AddSingleton<JwtAuthTokenServiceConfig>();
        services.AddSingleton<HashServiceConfig>();
        services.AddSingleton<UserServiceConfig>();
        services.AddSingleton<CacheCheckpointTrackerConfig>();
        services.AddSingleton<LiveEventQueueConfig>();
        services.AddSingleton<DbWriterConfig>();
        services.AddSingleton<ModuleServiceConfig>();
        services.AddSingleton<EventTrackerConfig>();

        services.AddSingleton<IModuleService, ModuleService>();
        services.AddSingleton<ModuleMessageAdder>((p) => (m, r) =>
        {
            //This is EXCEPTIONALLY inefficient: a new database context (not to mention other services)
            //will need to be created EVERY TIME someone sends a module message. That is awful...
            //I mean it's not MUCH worse IF the module is only sending a single message... eh, if you
            //notice bad cpu usage, go fix this.
            var creator = p.CreateScope().ServiceProvider.GetService<IDbWriter>() ?? throw new InvalidOperationException("No db writer for modules!!");

            try {
                creator.WriteAsync(m, r).Wait();
            }
            catch(AggregateException ex) {
                ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            }
        });

        //NOTE: do NOT just add all configs to the service! Only configs which have 
        //reasonable defaults! For instance: the EmailConfig should NOT be added!
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