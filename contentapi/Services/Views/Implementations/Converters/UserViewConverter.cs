using System;
using System.Linq;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Views.Implementations
{
    public class UserSearch : BaseSearch
    {
        public string Username {get;set;}
    }

    public class UserViewSourceProfile : Profile
    {
        public UserViewSourceProfile()
        {
            CreateMap<UserSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Username));
        }
    }

    public class UserViewSource : BaseEntityViewSource, IViewSource<UserViewFull, EntityPackage, EntityGroup, UserSearch>
    {
        protected IPermissionService service;

        public override string EntityType => Keys.UserType;

        public UserViewSource(ILogger<BaseViewSource> logger, IMapper mapper, IEntityProvider provider, IPermissionService service) 
            : base(logger, mapper, provider) 
        { 
            this.service = service;
        }

        public UserViewFull ToView(EntityPackage user)
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

            if(user.HasValue(Keys.AvatarKey))
                result.avatar = long.Parse(user.GetValue(Keys.AvatarKey).value);
            if(user.HasValue(Keys.RegistrationCodeKey))
                result.registrationKey = user.GetValue(Keys.RegistrationCodeKey).value;

            return result;
        }

        public EntityPackage FromView(UserViewFull user)
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
                .Add(NewValue(Keys.EmailKey, user.email))
                .Add(NewValue(Keys.PasswordSaltKey, user.salt))
                .Add(NewValue(Keys.PasswordHashKey, user.password));
            this.ApplyFromEditView(user, newUser, Keys.UserType);
            //Can't do anything about super
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(Keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }

        public IQueryable<long> SearchIds(UserSearch search, Func<IQueryable<EntityGroup>, IQueryable<EntityGroup>> modify = null)
        {
            var query = GetBaseQuery(search);

            if(modify != null)
                query = modify(query);

           //Special sorting routines go here

            return FinalizeQuery(query, search, x => x.entity.id);
        }
    }
}