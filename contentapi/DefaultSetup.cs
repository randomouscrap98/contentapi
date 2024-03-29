using System.Data;
using Amazon.S3;
using AutoMapper;
using blog_generator;
using contentapi.History;
using contentapi.Live;
using contentapi.Main;
using contentapi.Module;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using Dapper;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace contentapi.Setup;

public static class DefaultSetup
{
    private static bool OneTimeRun = false;
    private static readonly object OneTimeLock = new object();

    public static bool OneTimeSetup()
    {
        lock(OneTimeLock)
        {
            if(OneTimeRun)
                return false;

            SqlMapper.RemoveTypeMap(typeof(DateTime)); 
            SqlMapper.AddTypeHandler(typeof(DateTime), new DapperUtcDateTimeHandler());

            return true;
        }
    }

    /// <summary>
    /// Add all default service implementations to the given service container. Can override later of course.
    /// </summary>
    /// <remarks>
    /// To replace services (such as for unit tests), you can do: services.Replace(ServiceDescriptor.Transient<IFoo, FooB>());i
    /// </remarks>
    /// <param name="services"></param>
    public static void AddDefaultServices(IServiceCollection services, Func<IDbConnection> connectionProvider, IConfiguration configuration, IValueStore storage) //? configuration = null)
    {
        services.AddAutoMapper(typeof(ContentHistorySnapshotProfile));
        services.AddAutoMapper(typeof(SearchRequestPlusProfile)); //You can pick ANY profile, it just needs some type from this binary

        //var storageCon = configuration.GetConnectionString("storage");
        //if(string.IsNullOrWhiteSpace(storageCon)) storageCon = "Data Source=valuestore;Mode=Memory;Cache=Shared";
        //services.AddSingleton<IValueStore>(p => new SimpleSqliteValueStore(storageCon, p.GetRequiredService<ILogger<SimpleSqliteValueStore>>()));
        services.AddSingleton<IValueStore>(storage);

        //The factory itself is a singleton, but the things it creates aren't. All things created by the factory
        //have user-controlled lifetimes, you MUST dispose them when you create them!
        services.AddSingleton<IDbServicesFactory>(p => 
        {
            var factory = new ConfigurableDbServicesFactory() {
                DbConnectionCreator = () => { var c = connectionProvider(); c.Open(); return c; },
                GenericSearchCreator = () => new GenericSearcher(p.GetRequiredService<ILogger<GenericSearcher>>(), connectionProvider(),
                    p.GetRequiredService<IViewTypeInfoService>(), p.GetRequiredService<GenericSearcherConfig>(), p.GetRequiredService<IMapper>(),
                    p.GetRequiredService<IQueryBuilder>(), p.GetRequiredService<IPermissionService>()),
            };
            factory.DbWriterCreator = () => new DbWriter(p.GetRequiredService<ILogger<DbWriter>>(), factory.CreateSearch(),
                connectionProvider(), p.GetRequiredService<IViewTypeInfoService>(), p.GetRequiredService<IMapper>(),
                p.GetRequiredService<IHistoryConverter>(), p.GetRequiredService<IPermissionService>(), 
                p.GetRequiredService<ILiveEventQueue>(), p.GetRequiredService<DbWriterConfig>(), p.GetRequiredService<IRandomGenerator>(),
                p.GetRequiredService<IUserService>());
            return factory;
        });
        services.AddSingleton(p => new S3Provider() { GetRawProvider = new Func<IAmazonS3>(() => p.GetService<IAmazonS3>() ?? throw new InvalidOperationException("Couldn't create IAmazonS3!")) } );

        services.AddSingleton<IHistoryConverter, HistoryConverter>();
        //WARN: THIS HAS TO BE CREATED DIRECTLY!!!
        services.AddSingleton<IRuntimeInformation>(new MyRuntimeInformation(DateTime.Now)); //, p.GetRequiredService<IValueStore>()));
        services.AddSingleton<IViewTypeInfoService, ViewTypeInfoService_Cached>();
        services.AddSingleton<IQueryBuilder, QueryBuilder>();
        services.AddSingleton<ISearchQueryParser, SearchQueryParser>();
        services.AddSingleton(typeof(IAuthTokenService<>), typeof(JwtAuthTokenService<>));
        services.AddSingleton(typeof(ICacheCheckpointTracker<>), typeof(CacheCheckpointTracker<>));
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<ILiveEventQueue, LiveEventQueue>();
        services.AddSingleton<IEventTracker, EventTracker>();
        services.AddSingleton<IUserStatusTracker, UserStatusTracker>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IModuleService, ModuleService>();

        //Unfortunately, need to build some of these singletons "special"
        services.AddSingleton<ICacheCheckpointTracker<LiveEvent>>(p =>
        {
            var storage = p.GetRequiredService<IValueStore>();
            var result = ActivatorUtilities.GetServiceOrCreateInstance<CacheCheckpointTracker<LiveEvent>>(p);
            result.UniqueSessionId = storage.Get<int>(Constants.StorageKeys.restarts.ToString(), 0) % result.config.CacheIdIncrement;
            result.logger.LogInformation($"Current Session id: {result.UniqueSessionId}, increment: {result.config.CacheIdIncrement}");
            return result;
        });

        //Non-interface weirdness, may change later
        services.AddSingleton<TemplateLoader>();

        var emailType = configuration.GetValue<string>("EmailSender");
        var imageManipulator = configuration.GetValue<string>("ImageManipulator");

        if(emailType == "functional")
            services.AddSingleton<IEmailService, EmailService>();
        else
            services.AddSingleton<IEmailService, FileEmailService>();

        if(imageManipulator == "imagick")
            services.AddSingleton<IImageManipulator, ImageManipulator_IMagick>();
        else
            services.AddSingleton<IImageManipulator, ImageManipulator_Direct>();

        //This NEEDS to stay transient because it holds onto a DB connection! We want those recycled!
        services.AddTransient<ShortcutsService>();

        //Configs (these have default values given in configs)
        AddConfigBinding<GenericSearcherConfig>(services, configuration);
        AddConfigBinding<JwtAuthTokenServiceConfig>(services, configuration);
        AddConfigBinding<HashServiceConfig>(services, configuration);
        AddConfigBinding<UserServiceConfig>(services, configuration);
        AddConfigBinding<CacheCheckpointTrackerConfig>(services, configuration);
        AddConfigBinding<LiveEventQueueConfig>(services, configuration);
        AddConfigBinding<DbWriterConfig>(services, configuration);
        AddConfigBinding<ModuleServiceConfig>(services, configuration);
        AddConfigBinding<EventTrackerConfig>(services, configuration);
        AddConfigBinding<FileServiceConfig>(services, configuration);
        AddConfigBinding<QueryBuilderConfig>(services, configuration);
        AddConfigBinding<TemplateConfig>(services, configuration);

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