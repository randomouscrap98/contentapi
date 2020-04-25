using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Services;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Implementations;

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
            var dbConfig = new DatabaseConfig();
            Configuration.Bind(nameof(DatabaseConfig), dbConfig);

            var tokenSection = Configuration.GetSection(nameof(TokenServiceConfig));

            services.Configure<EmailConfig>(Configuration.GetSection(nameof(EmailConfig)))
                    .Configure<LanguageConfig>(Configuration.GetSection(nameof(LanguageConfig)))
                    .Configure<SystemConfig>(Configuration.GetSection(nameof(SystemConfig)))
                    .Configure<FileControllerConfig>(Configuration.GetSection(nameof(FileControllerConfig)))
                    .Configure<UserControllerConfig>(Configuration.GetSection(nameof(UserControllerConfig)))
                    .Configure<TokenServiceConfig>(tokenSection);

            services.AddSingleton(new HashConfig());    //Just use defaults

            var keys = new Keys();
            keys.EnsureAllUnique();
            services.AddSingleton(keys);

            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<ILanguageService, LanguageService>();
            services.AddTransient<ITokenService, TokenService>();
            services.AddTransient<IHashService, HashService>();
            services.AddTransient(typeof(IDecayer<>), typeof(Decayer<>));

            services.AddTransient<ControllerServices>();

            var provider = new DefaultServiceProvider();
            provider.AddDefaultServices(services, options => 
                {
                    if(dbConfig.DbType == "sqlite")
                    {
                        options.UseSqlite(dbConfig.ConnectionString);
                    }
                    else if(dbConfig.DbType == "mysql")
                    {
                        options.UseMySql(dbConfig.ConnectionString);
                    }

                    options.EnableSensitiveDataLogging(dbConfig.SensitiveLogging);
                }
            );

            //WE need a singleton signaler: we'll all be using the same thing across different providers
            services.AddSingleton<ISignaler<EntityBase>, SignalSystem<EntityBase>>();

            services.AddCors();
            services.AddControllers()
                    .AddJsonOptions(options=> options.JsonSerializerOptions.Converters.Add(new TimeSpanToStringConverter()));
            services.AddAutoMapper(typeof(Startup));

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
            if(Configuration.GetValue<bool>("ShowExceptions"))
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/error");
            

            //Wide open for now, this might need to be changed later.
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

            app.UseHttpsRedirection();

            app.UseRouting();

            //Apparently authentication has to come before authorization
            app.UseAuthentication();    
            app.UseAuthorization();

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
