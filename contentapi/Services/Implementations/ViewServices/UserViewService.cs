using contentapi.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using AutoMapper;
using contentapi.Services.Extensions;
using Randomous.EntitySystem.Extensions;
using contentapi.Services.Constants;
using System;
using Randomous.EntitySystem;
using System.Linq;

namespace contentapi.Services.Implementations
{
    public class UserControllerProfile : Profile
    {
        public UserControllerProfile()
        {
            CreateMap<UserViewBasic, UserView>().ReverseMap();
            CreateMap<UserViewBasic, UserViewFull>().ReverseMap();
            CreateMap<UserView, UserViewFull>().ReverseMap();
            CreateMap<UserViewFull, UserViewFull>(); //Simple object mapping
            CreateMap<UserCredential, UserViewFull>().ReverseMap();
        }
    }

    public class UserGroupHideData
    {
        public List<UserHideData> hides {get;set;} = new List<UserHideData>();
    }

    public class UserHideData
    {
        public long userId;
        public List<long> hides = new List<long>();
    }

    public class UserViewService : BaseEntityViewService<UserViewFull, UserSearch>
    {
        protected IHashService hashService;
        protected ITokenService tokenService;
        protected ILanguageService languageService;
        protected IEmailService emailService;
        protected CacheService<string, UserGroupHideData> hidecache;

        public UserViewService(ILogger<UserViewService> logger, ViewServicePack services, IHashService hashService,
            ITokenService tokenService, ILanguageService languageService, IEmailService emailService,
            UserViewSource converter, CacheService<string, UserGroupHideData> hidecache)
            :base(services, logger, converter)
        { 
            this.hidecache = hidecache;
            this.hashService = hashService;
            this.tokenService = tokenService;
            this.languageService = languageService;
            this.emailService = emailService;
        }

        public UserViewSource Source => (UserViewSource)converter;

        public override string EntityType => Keys.UserType;

        public override async Task<UserViewFull> CleanViewGeneralAsync(UserViewFull view, Requester requester)
        {
            view = await base.CleanViewGeneralAsync(view, requester);

            //Go look up the avatar. Make sure it's A FILE DAMGIT
            var file = await provider.FindByIdBaseAsync(view.avatar);

            if(file != null && (!file.type.StartsWith(Keys.FileType) || !file.content.ToLower().StartsWith("image")))
                throw new BadRequestException("Avatar isn't an image type content!");

            return view;
        }

        public async Task<UserViewFull> WriteSpecialAsync(long id, Requester requester, Action<EntityPackage> modify)
        {
            var original = await services.provider.FindByIdAsync(id);
            modify(original);
            original.Relink(); //Probably safe... I hope
            await services.provider.WriteAsync(original);
            hidecache.PurgeCache(); //Yes, the whole thing. Oh well
            return converter.ToView(original);
        }

        public override async Task<UserViewFull> WriteAsync(UserViewFull view, Requester requester)//, bool history = true)
        {
            hidecache.PurgeCache(); //Yes, the whole thing. Oh well
            return converter.ToView(await WriteViewBaseAsync(view, requester, (p) =>
            {
                //Before creating the user, we need to set the owner as themselves, not as anonymous.
                var creatorRelation = p.GetRelation(Keys.CreatorRelation);
                creatorRelation.entityId1 = creatorRelation.entityId2;
                creatorRelation.value = creatorRelation.entityId2.ToString(); //Warn: this is VERY implementation specific! Kinda sucks to have two pieces of code floating around!
            }));
        }

        public override Task<UserViewFull> DeleteAsync(long entityId, Requester requester)
        {
            hidecache.PurgeCache();
            return base.DeleteAsync(entityId, requester);
        }

        public async Task<UserGroupHideData> GetUserHideDataAsync(IEnumerable<long> users)
        {
            UserGroupHideData result = null;
            var search = new EntityValueSearch() { EntityIds = users.Distinct().OrderBy(x => x).ToList(), KeyLike = Keys.UserHideKey };
            var key = string.Join(",", search.EntityIds);
            if(hidecache.GetValue(key, ref result))
                return result;
            result = new UserGroupHideData() { hides = (await provider.GetEntityValuesAsync(search)).Select(x => 
                new UserHideData() {
                    userId = x.entityId,
                    hides = x.value.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x)).ToList()
                }).ToList() };
            hidecache.StoreItem(key, result);
            return result;
        }

        public async Task<UserViewFull> FindByUsernameAsync(string username, Requester requester)
        {
            return (await SearchAsync(new UserSearch()
            {
                Usernames = new List<string>() { username }
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

        public override Task<List<UserViewFull>> PreparedSearchAsync(UserSearch search, Requester requester)
        {
            //NO permissions check! All users are readable!
            return converter.SimpleSearchAsync(search);
        }
    }
}