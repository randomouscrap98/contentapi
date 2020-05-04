using contentapi.Views;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using AutoMapper;
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace contentapi.Services.Implementations
{
    public class UserSearch : EntitySearchBase
    {
        public string Username {get;set;}
    }

    public class UserControllerProfile : Profile
    {
        public UserControllerProfile()
        {
            CreateMap<UserSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));

            CreateMap<UserViewBasic, UserView>().ReverseMap();
            CreateMap<UserViewBasic, UserViewFull>().ReverseMap();
            CreateMap<UserView, UserViewFull>().ReverseMap();
            CreateMap<UserCredential, UserViewFull>().ReverseMap();
        }
    }

    public class UserViewService : BaseEntityViewService<UserViewFull, UserSearch>
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;

        public UserViewService(ILogger<UserViewService> logger, ViewServices services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService)
            :base(services, logger)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
        }

        public override string EntityType => keys.UserType;

        public override UserViewFull CreateBaseView(EntityPackage user)
        {
            var result = new UserViewFull() 
            { 
                username = user.Entity.name, 
                email = user.GetValue(keys.EmailKey).value, 
                super = services.permissions.IsSuper(user.Entity.id),
                password = user.GetValue(keys.PasswordHashKey).value,
                salt = user.GetValue(keys.PasswordSaltKey).value
            };

            if(user.HasValue(keys.AvatarKey))
                result.avatar = long.Parse(user.GetValue(keys.AvatarKey).value);
            if(user.HasValue(keys.RegistrationCodeKey))
                result.registrationKey = user.GetValue(keys.RegistrationCodeKey).value;

            return result;
        }

        public override EntityPackage CreateBasePackage(UserViewFull user)
        {
            var newUser = NewEntity(user.username)
                .Add(NewValue(keys.AvatarKey, user.avatar.ToString()))
                .Add(NewValue(keys.EmailKey, user.email))
                .Add(NewValue(keys.PasswordSaltKey, user.salt))
                .Add(NewValue(keys.PasswordHashKey, user.password));
            //Can't do anything about super
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }

        public override async Task<UserViewFull> CleanViewGeneralAsync(UserViewFull view, long userId)
        {
            view = await base.CleanViewGeneralAsync(view, userId);

            //Go look up the avatar. Make sure it's A FILE DAMGIT
            var file = await provider.FindByIdBaseAsync(view.avatar);

            if(file != null && (!file.type.StartsWith(keys.FileType) || !file.content.ToLower().StartsWith("image")))
                throw new BadRequestException("Avatar isn't an image type content!");

            return view;
        }

        public override async Task<IList<UserViewFull>> SearchAsync(UserSearch search, ViewRequester requester)
        {
            logger.LogDebug($"User SearchAsync called by {requester}");
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));
            return (await provider.GetEntityPackagesAsync(entitySearch)).Select(x => ConvertToView(x)).ToList();
        }

        public override async Task<UserViewFull> WriteAsync(UserViewFull view, ViewRequester requester)
        {
            return ConvertToView(await WriteViewBaseAsync(view, requester.userId, (p) =>
            {
                //Before creating the user, we need to set the owner as themselves, not as anonymous.
                var creatorRelation = p.GetRelation(keys.CreatorRelation);
                creatorRelation.entityId1 = creatorRelation.entityId2;
                creatorRelation.value = creatorRelation.entityId2.ToString(); //Warn: this is VERY implementation specific! Kinda sucks to have two pieces of code floating around!
            }));
        }

        public async Task<UserViewFull> FindByUsernameAsync(string username, ViewRequester requester)
        {
            return (await SearchAsync(new UserSearch()
            {
                Username = username
            }, requester)).OnlySingle();
        }

        protected async Task<UserViewFull> FindByExactValueAsync(string key, string value, ViewRequester requester)
        {
            var foundValue = await FindValueAsync(key, value);

            if(foundValue == null)
                return null;

            return await FindByIdAsync(foundValue.entityId, requester);
        }

        public Task<UserViewFull> FindByEmailAsync(string email, ViewRequester requester)
        {
            return FindByExactValueAsync(keys.EmailKey, email, requester);
        }

        public Task<UserViewFull> FindByRegistration(string registrationKey, ViewRequester requester)
        {
            return FindByExactValueAsync(keys.RegistrationCodeKey, registrationKey, requester);
        }
    }
}