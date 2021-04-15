using System;
using System.Collections.Generic;
using AutoMapper;
using contentapi.Configs;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class DefaultServiceProvider
    {
        /// <summary>
        /// A class specifically to allow an essentially generic function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class HackOptions<T> where T : class
        {
            public HackOptions(IServiceCollection services, IConfiguration config)
            {
                var section = config.GetSection(typeof(T).Name);

                services.Configure<T>(section);
                services.AddTransient<T>(p => 
                    p.GetService<IOptionsMonitor<T>>().CurrentValue);
            }
        };

        public void AddDefaultServices(IServiceCollection services)
        {
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<ILanguageService, LanguageService>();
            services.AddTransient<ITokenService, TokenService>();
            services.AddTransient<IHashService, HashService>();
            services.AddTransient<IPermissionService, PermissionService>();
            services.AddTransient<IHistoryService, HistoryService>();
            services.AddSingleton(typeof(IDecayer<>), typeof(Decayer<>)); //Is it safe to make ALL decayers singletons? I don't know... I suppose that's what it's for?
            services.AddTransient(typeof(ITempTokenService<>), typeof(TempTokenService<>));
            services.AddSingleton<UserValidationService>();
            services.AddSingleton<ICodeTimer, SimpleCodeTimer>();

            //TODO: eventually, make this configurable? But how, not all the cache will be good!
            services.AddSingleton<CacheServiceConfig>();
            services.AddSingleton(typeof(CacheService<,>));
            //services.AddSingleton<CacheService<string, List<CategoryView>>>();
            //services.AddSingleton<CacheService<string, List<ModuleView>>>();
            //services.AddSingleton<CacheService<string, UserGroupHideData>>();
            //services.AddSingleton<CacheService<long, CommentView>>();
            //services.AddSingleton<CacheService<long, ModuleView>>();

            services.AddTransient<BaseViewSourceServices>();

            services.AddTransient<ActivityViewService>();
            services.AddTransient<BanViewService>();
            services.AddTransient<CategoryViewService>();
            services.AddTransient<CommentViewService>();
            services.AddTransient<ContentViewService>();
            services.AddTransient<FileViewService>();
            services.AddTransient<UserViewService>();
            services.AddTransient<WatchViewService>();
            services.AddTransient<VoteViewService>();
            services.AddTransient<ModuleViewService>();
            services.AddTransient<ModuleMessageViewService>();

            //Special services
            services.AddTransient<RelationListenerService>();
            services.AddTransient<ChainService>();
            services.AddSingleton<IModuleService, ModuleService>();
            services.AddSingleton<ModuleMessageAdder>((p) => (m) =>
            {
                //This is EXCEPTIONALLY inefficient: a new database context (not to mention other services)
                //will need to be created EVERY TIME someone sends a module message. That is awful...
                //I mean it's not MUCH worse IF the module is only sending a single message... eh, if you
                //notice bad cpu usage, go fix this.
                var creator = p.CreateScope().ServiceProvider.GetService<ModuleMessageViewService>();
                creator.AddMessageAsync(m).Wait();
            });

            services.AddTransient<ActivityViewSource>();
            services.AddTransient<BanViewSource>();
            services.AddTransient<CategoryViewSource>();
            services.AddTransient<CommentViewSource>();
            services.AddTransient<ContentViewSource>();
            services.AddTransient<FileViewSource>();
            services.AddTransient<UserViewSource>();
            services.AddTransient<WatchViewSource>();
            services.AddTransient<VoteViewSource>();
            services.AddTransient<ModuleViewSource>();
            services.AddTransient<ModuleMessageViewSource>();

            services.AddTransient((p) => new ChainServices()
            {
                file = p.GetService<FileViewService>(),
                user = p.GetService<UserViewService>(),
                content = p.GetService<ContentViewService>(),
                category = p.GetService<CategoryViewService>(),
                comment = p.GetService<CommentViewService>(),
                activity = p.GetService<ActivityViewService>(),
                watch = p.GetService<WatchViewService>(),
                vote = p.GetService<VoteViewService>(),
                module = p.GetService<ModuleViewService>(),
                ban = p.GetService<BanViewService>(),
                modulemessage = p.GetService<ModuleMessageViewService>()
            });

            //We need automapper for our view services
            services.AddAutoMapper(GetType());

            //And now, the service config that goes into EVERY controller.
            services.AddTransient<ViewServicePack>();

            //Just always good to be safe!
            Keys.EnsureAllUnique();
        }

        public void AddConfiguration<T>(IServiceCollection services, IConfiguration config) where T : class
        {
            new HackOptions<T>(services, config);
        }

        public void AddServiceConfigurations(IServiceCollection services, IConfiguration config)
        {
            AddConfiguration<EmailConfig>(services, config);
            AddConfiguration<LanguageConfig>(services, config);
            AddConfiguration<SystemConfig>(services, config);
            AddConfiguration<TempTokenServiceConfig>(services, config);
            AddConfiguration<TokenServiceConfig>(services, config);
            AddConfiguration<ChainServiceConfig>(services, config);
            AddConfiguration<ModuleServiceConfig>(services, config);
            AddConfiguration<RelationListenerServiceConfig>(services, config);
            AddConfiguration<SimpleCodeTimerConfig>(services, config);
            //AddConfiguration<DocumentationConfig>(services, config);
            services.AddSingleton<HashConfig>();
        }
    }
}