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
using contentapi.Services.Constants;
using contentapi.Services.ViewConversion;

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

        public UserViewService(ILogger<UserViewService> logger, ViewServicePack services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService,
            UserViewConverter converter)
            :base(services, logger, converter)
        { 
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
        }

        public override string EntityType => Keys.UserType;

        //public override UserViewFull CreateBaseView(EntityPackage user)
        //{
        //    var result = new UserViewFull() 
        //    { 
        //        username = user.Entity.name, 
        //        email = user.GetValue(Keys.EmailKey).value, 
        //        super = services.permissions.IsSuper(user.Entity.id),
        //        password = user.GetValue(Keys.PasswordHashKey).value,
        //        salt = user.GetValue(Keys.PasswordSaltKey).value
        //    };

        //    if(user.HasValue(Keys.AvatarKey))
        //        result.avatar = long.Parse(user.GetValue(Keys.AvatarKey).value);
        //    if(user.HasValue(Keys.RegistrationCodeKey))
        //        result.registrationKey = user.GetValue(Keys.RegistrationCodeKey).value;

        //    return result;
        //}

        //public override EntityPackage CreateBasePackage(UserViewFull user)
        //{
        //    var newUser = NewEntity(user.username)
        //        .Add(NewValue(Keys.AvatarKey, user.avatar.ToString()))
        //        .Add(NewValue(Keys.EmailKey, user.email))
        //        .Add(NewValue(Keys.PasswordSaltKey, user.salt))
        //        .Add(NewValue(Keys.PasswordHashKey, user.password));
        //    //Can't do anything about super
        //    
        //    if(!string.IsNullOrWhiteSpace(user.registrationKey))
        //        newUser.Add(NewValue(Keys.RegistrationCodeKey, user.registrationKey));

        //    return newUser;
        //}

        public override async Task<UserViewFull> CleanViewGeneralAsync(UserViewFull view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            //Go look up the avatar. Make sure it's A FILE DAMGIT
            var file = await provider.FindByIdBaseAsync(view.avatar);

            if(file != null && (!file.type.StartsWith(Keys.FileType) || !file.content.ToLower().StartsWith("image")))
                throw new BadRequestException("Avatar isn't an image type content!");

            return view;
        }

        public override async Task<IList<UserViewFull>> SearchAsync(UserSearch search, Requester requester)
        {
            logger.LogDebug($"User SearchAsync called by {requester}");
            var entitySearch = ModifySearch(services.mapper.Map<EntitySearch>(search));
            return (await provider.GetEntityPackagesAsync(entitySearch)).Select(x => converter.ToView(x)).ToList();
        }

        public override async Task<UserViewFull> WriteAsync(UserViewFull view, Requester requester)
        {
            return converter.ToView(await WriteViewBaseAsync(view, requester, (p) =>
            {
                //Before creating the user, we need to set the owner as themselves, not as anonymous.
                var creatorRelation = p.GetRelation(Keys.CreatorRelation);
                creatorRelation.entityId1 = creatorRelation.entityId2;
                creatorRelation.value = creatorRelation.entityId2.ToString(); //Warn: this is VERY implementation specific! Kinda sucks to have two pieces of code floating around!
            }));
        }

        public async Task<UserViewFull> FindByUsernameAsync(string username, Requester requester)
        {
            return (await SearchAsync(new UserSearch()
            {
                Username = username
            }, requester)).OnlySingle();
        }

        protected async Task<UserViewFull> FindByExactValueAsync(string key, string value, Requester requester)
        {
            var foundValue = await FindValueAsync(key, value);

            if(foundValue == null)
                return null;

            return await FindByIdAsync(foundValue.entityId, requester);
        }

        public Task<UserViewFull> FindByEmailAsync(string email, Requester requester)
        {
            return FindByExactValueAsync(Keys.EmailKey, email, requester);
        }

        public Task<UserViewFull> FindByRegistration(string registrationKey, Requester requester)
        {
            return FindByExactValueAsync(Keys.RegistrationCodeKey, registrationKey, requester);
        }
    }
}