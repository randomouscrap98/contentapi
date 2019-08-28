using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Controllers;
using contentapi.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            services.AddSingleton(new UsersControllerConfig()
            {
                JwtSecretKey = tempSection.GetValue<string>("JWTSecret")
            });

            //Database config
            services.AddDbContext<ContentDbContext>(options => options.UseSqlite(contentConstring));

            //Mapping config
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<User,UserView>();
            }); 
            services.AddSingleton(mapperConfig.CreateMapper());

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
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

            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
