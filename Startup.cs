using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Controllers;
using contentapi.Models;
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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var dataSection = Configuration.GetSection("Data");
            var tempSection = Configuration.GetSection("Temp");
            var contentConstring = dataSection.GetValue<string>("ContentConnectionString");
            var secretKey = tempSection.GetValue<string>("JWTSecret");
            var secretKeyBytes = Encoding.ASCII.GetBytes(secretKey);

            services.AddSingleton(new UsersControllerConfig()
            {
                JwtSecretKey = secretKey
            });
            services.AddSingleton(new EmailConfig()
            {
                User = tempSection.GetValue<string>("smtpEmail"),
                Password = tempSection.GetValue<string>("smtpPassword"),
                Port = tempSection.GetValue<int>("smtpPort"),
                Host = tempSection.GetValue<string>("smtpHost"),
                SubjectFront = tempSection.GetValue<string>("emailSubjectFront")
            });

            //Database config
            services.AddDbContext<ContentDbContext>(options => options.UseLazyLoadingProxies().UseSqlite(contentConstring));

            //Mapping config
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<User,UserView>();
                //cfg.CreateMap<Category,CategoryView>().ForMember(dest => dest.childrenIds, opt => opt.MapFrom(src => src.Children.Select(x => x.id)));
                cfg.CreateMap<Category, CategoryView>();
                cfg.CreateMap<CategoryView,Category>();
                //cfg.CreateMap<
            }); 
            services.AddSingleton(mapperConfig.CreateMapper());

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
