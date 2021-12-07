using System.Collections.Generic;
using System.Text;
using AspNetCoreRateLimit;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Randomous.EntitySystem.Implementations;
using Serilog;
using System;
using Newtonsoft.Json;
using contentapi.Db;
using Microsoft.Data.Sqlite;
using contentapi.Db.History;

namespace contentapi
{
    public class Startup
    {
        //private static Timer ServiceTimer = null;
        //private static SimpleCodeTimer Timer = null;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public T AddSingletonConfig<T>(IConfiguration configuration, IServiceCollection services, string key) where T : class, new()
        {
            T config = new T();
            configuration.Bind(key, config);
            services.AddSingleton(config);
            return config;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //Add the services from entity system.
            var provider = new Randomous.EntitySystem.Implementations.DefaultServiceProvider();

            var dbConfig = new DatabaseConfig();
            Configuration.Bind(nameof(DatabaseConfig), dbConfig);

            provider.AddDefaultServices(services, options => 
            {
                if(dbConfig.DbType == "sqlite")
                    options.UseSqlite(dbConfig.ConnectionString);
                else if(dbConfig.DbType == "mysql")
                    options.UseMySql(dbConfig.ConnectionString, options => options.EnableRetryOnFailure());

                options.EnableSensitiveDataLogging(dbConfig.SensitiveLogging);
            });

            Db.Setup.DefaultSetup.AddDefaultServices(services);

            //Fix some entity system stuff. We need singletons but the default is transient
            //The IEntityProvider that we give out needs to have only a single access
            services.AddSingleton(new EntityQueryableEfCoreConfig() { ConcurrentAccess = 1 });

            //Add our own services from contentapi
            var contentApiDefaultProvider = new Services.Implementations.DefaultServiceProvider();
            contentApiDefaultProvider.AddDefaultServices(services);
            contentApiDefaultProvider.AddServiceConfigurations(services, Configuration);
            contentApiDefaultProvider.AddConfiguration<FileControllerConfig>(services, Configuration);
            contentApiDefaultProvider.AddConfiguration<UserControllerConfig>(services, Configuration);
            contentApiDefaultProvider.AddConfiguration<NewConvertControllerConfig>(services, Configuration);

            //Also a singleton for the token system which we'll use for websockets
            services.AddSingleton<ITempTokenService<long>, TempTokenService<long>>();

            services.AddTransient<BaseSimpleControllerServices>();

            services.AddTransient<ContentApiDbConnection>(ctx => new Db.ContentApiDbConnection(new SqliteConnection("Data Source=newcontent.db")));

            //The rest is http stuff I think

            //Rate limiting, hope this works!
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            Action<Newtonsoft.Json.JsonSerializerSettings> setupJsonOptions = options =>
            {
                options.Converters.Add(new CustomDateTimeConverter());
                options.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            };
            services.AddSingleton(setupJsonOptions);
            JsonConvert.DefaultSettings = () => {
                var settings = new JsonSerializerSettings();
                setupJsonOptions(settings);
                return settings;
            };

            services.AddCors();
            services.AddControllers()
                    .AddNewtonsoftJson(options => setupJsonOptions(options.SerializerSettings));

            //other rate limiting stuff
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

            //Added automapper here before

            var tokenSection = Configuration.GetSection(nameof(TokenServiceConfig));

            //This is all that JWT junk. I hope it still works like this... I just copied this from my core 2.0 project
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSection.GetValue<string>("SecretKey"))),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

            AddSwagger(services);

            //services.AddSingleton<ICodeTimer>(p =>
            //{
            //    if(Timer == null)
            //    {
            //        Timer = new SimpleCodeTimer();
            //        var logger = p.GetService<ILogger>();
            //        var serviceConfig = Configuration.GetSection("ServiceTimerConfig");
            //        var delay = serviceConfig.GetValue<TimeSpan>("Delay"); 
            //        var interval = serviceConfig.GetValue<TimeSpan>("Interval");
            //        var path = serviceConfig.GetValue<string>("ProfilerPath");
            //        logger.Information($"Starting profiler: Delay {delay} Interval {interval} Path {path}");

            //        ServiceTimer = new Timer(x => 
            //        {
            //            try { Timer.FlushData(path).Wait(); }
            //            catch(Exception ex) { logger.Warning($"Couldn't flush profiler: {ex}"); }
            //        }, null, delay, interval);
            //    }

            //    return Timer;
            //});
        }

        public void AddSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "New SBS API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"Bearer [space] token",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    { 
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header,
                        },
                        new List<string>()
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //Wide open for now, this might need to be changed later.
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("*")
                .SetPreflightMaxAge(TimeSpan.FromHours(6)));

            if(Configuration.GetValue<bool>("ShowExceptions"))
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/error");
            
            app.UseIpRateLimiting();
            app.UseSerilogRequestLogging();

            app.UseRouting();

            //Apparently authentication has to come before authorization
            app.UseAuthentication();    
            app.UseAuthorization();

            //MUST COME BEFORE USE ENDPOINTS 
            //https://www.koskila.net/httpcontext-websockets-iswebsocketrequest-always-null-in-your-net-core-code/
            app.UseWebSockets(new WebSocketOptions()
            {
                //Make this configurable later?
                KeepAliveInterval = TimeSpan.FromSeconds(15)
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //Swagger is the API documentation
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "New SBS API");
            });
        }

    }
}
