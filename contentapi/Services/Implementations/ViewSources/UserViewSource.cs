using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class UserSearch : BaseHistorySearch
    {
        public string UsernameLike {get;set;}
        public List<string> Usernames {get;set;} = new List<string>();
    }

    public class UserViewSourceProfile : Profile
    {
        public UserViewSourceProfile()
        {
            CreateMap<UserSearch, EntitySearch>()
                .ForMember(x => x.NameLike, o => o.MapFrom(s => s.UsernameLike))
                .ForMember(x => x.Names, o => o.MapFrom(s => s.Usernames));
        }
    }

    public class UserViewSource : BaseEntityViewSource<UserViewFull, EntityPackage, EntityGroup, UserSearch>
    {
        protected IPermissionService service;

        public override string EntityType => Keys.UserType;

        public UserViewSource(ILogger<UserViewSource> logger, IMapper mapper, IEntityProvider provider, IPermissionService service) 
            : base(logger, mapper, provider) 
        { 
            this.service = service;
        }

        public override UserViewFull ToView(EntityPackage user)
        {
            var result = new UserViewFull() 
            { 
                username = user.Entity.name, 
                email = user.GetValue(Keys.EmailKey).value, 
                super = service.IsSuper(user.Entity.id),
                password = user.GetValue(Keys.PasswordHashKey).value,
                salt = user.GetValue(Keys.PasswordSaltKey).value
            };

            this.ApplyToEditView(user, result);

            if(user.HasValue(Keys.UserSpecialKey))
                result.special = user.GetValue(Keys.UserSpecialKey).value;
            if(user.HasValue(Keys.AvatarKey))
                result.avatar = long.Parse(user.GetValue(Keys.AvatarKey).value);
            if(user.HasValue(Keys.RegistrationCodeKey))
                result.registrationKey = user.GetValue(Keys.RegistrationCodeKey).value;
            if(user.HasValue(Keys.UserHideKey))
                result.hidelist = user.GetValue(Keys.UserHideKey).value.Split(",".ToCharArray(),StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x)).ToList();
            
            result.registered = string.IsNullOrWhiteSpace(result.registrationKey);

            return result;
        }

        public override EntityPackage FromView(UserViewFull user)
        {
            var NewValue = new Func<string, string, EntityValue>((k,v) => new EntityValue()
            {
                entityId = user.id,
                createDate = null,
                key = k,
                value = v
            });

            var newUser = this.NewEntity(user.username)
                .Add(NewValue(Keys.AvatarKey, user.avatar.ToString()))
                .Add(NewValue(Keys.UserSpecialKey, user.special))
                .Add(NewValue(Keys.EmailKey, user.email))
                .Add(NewValue(Keys.PasswordSaltKey, user.salt))
                .Add(NewValue(Keys.UserHideKey, string.Join(",", user.hidelist)))
                .Add(NewValue(Keys.PasswordHashKey, user.password));
            this.ApplyFromEditView(user, newUser, EntityType);
            //Can't do anything about super
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(Keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }
    }
}