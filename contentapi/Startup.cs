using System.Collections.Generic;
using System.Text;
using AspNetCoreRateLimit;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Middleware;
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
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Implementations;
using Serilog;

namespace contentapi
{
    public class Startup
    {
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

            //Fix some entity system stuff. We need singletons but the default is transient
            //The IEntityProvider that we give out needs to have only a single access
            services.AddSingleton(new EntityQueryableEfCoreConfig() { ConcurrentAccess = 1 });
            //services.AddSingleton<ISignaler<EntityBase>, SignalSystem<EntityBase>>();

            //Add our own services from contentapi
            var contentApiDefaultProvider = new Services.Implementations.DefaultServiceProvider();
            contentApiDefaultProvider.AddDefaultServices(services);
            contentApiDefaultProvider.AddServiceConfigurations(services, Configuration);
            contentApiDefaultProvider.AddConfiguration<FileControllerConfig>(services, Configuration);
            contentApiDefaultProvider.AddConfiguration<UserControllerConfig>(services, Configuration);

            //Also a singleton for the token system which we'll use for websockets
            services.AddSingleton<ITempTokenService<long>, TempTokenService<long>>();

            //A special case for websockets: we determine what the websockets will handle right here and now
            services.AddSingleton<WebSocketMiddlewareConfig>((p) =>
            {
                var websocketConfig = new WebSocketMiddlewareConfig();
                var echoer = (WebSocketEcho)ActivatorUtilities.GetServiceOrCreateInstance(p, typeof(WebSocketEcho));
                websocketConfig.RouteHandlers.Add("testecho", echoer.Echo);
                return websocketConfig;
            });

            //The rest is http stuff I think

            //Rate limiting, hope this works!
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

            services.AddCors();
            services.AddControllers()
                    .AddJsonOptions(options=> options.JsonSerializerOptions.Converters.Add(new TimeSpanToStringConverter()))
                    .AddNewtonsoftJson();

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
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseWebSockets();

            app.UseMiddleware<WebSocketMiddleware>();

            //Swagger is the API documentation
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "New SBS API");
            });
        }

    }
}
