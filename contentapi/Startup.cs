using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace contentapi
{

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureBasicServices(IServiceCollection services, StartupServiceConfig config)
        {
            var secretKeyBytes = Encoding.ASCII.GetBytes(config.SecretKey);

            services.AddSingleton(config.HashConfig);
            services.AddSingleton(config.EmailConfig); 
            services.AddSingleton(config.LanguageConfig); 

            services.AddSingleton(new SessionConfig()
            {
                SecretKey = config.SecretKey
            });

            //Database config
            services.AddDbContext<ContentDbContext>(options => options.UseLazyLoadingProxies().UseSqlite(config.ContentConString));

            //Mapping config
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<User,UserView>();
                //Find a way to stop this duplicate code
                cfg.CreateMap<Category, CategoryView>().ForMember(dest => dest.accessList, opt => opt.MapFrom(src => src.AccessList.ToDictionary(x => x.userId, y => y.access)));
                cfg.CreateMap<CategoryView,Category>().ForMember(dest => dest.AccessList, opt => opt.MapFrom(src => src.accessList.Select(x => new CategoryAccess() 
                {
                    access = x.Value,
                    userId = x.Key,
                }).ToList()));
                cfg.CreateMap<Content, ContentView>().ForMember(dest => dest.accessList, opt => opt.MapFrom(src => src.AccessList.ToDictionary(x => x.userId, y => y.access)));
                cfg.CreateMap<ContentView, Content>().ForMember(dest => dest.AccessList, opt => opt.MapFrom(src => src.accessList.Select(x => new ContentAccess() 
                {
                    access = x.Value,
                    userId = x.Key,
                }).ToList()));
            }); 
            services.AddSingleton(mapperConfig.CreateMapper());

            //My own services
            services.AddTransient<PermissionService>()
                    .AddTransient<AccessService>()
                    .AddTransient<QueryService>()
                    .AddTransient<SessionService>()
                    .AddTransient<GenericControllerServices>();

            //REAL interfaced services
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<ILanguageService, LanguageService>();
            services.AddTransient<IHashService, HashService>();

            services.AddCors();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var dataSection = Configuration.GetSection("Data");
            var tempSection = Configuration.GetSection("Temp");

            var config = new StartupServiceConfig()
            {
                SecretKey = tempSection.GetValue<string>("JWTSecret"),
                ContentConString = dataSection.GetValue<string>("ContentConnectionString")
            };

            //Assign the email config from the "Email" section (must exist prior, see how we create an object above)
            Configuration.Bind("Email", config.EmailConfig);
            Configuration.Bind("Language", config.LanguageConfig);

            ConfigureBasicServices(services, config);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //Wide open??? Fix this later maybe!!!
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            app.UseAuthentication();
            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
