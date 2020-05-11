using System;
using contentapi.Services.Constants;
using contentapi.Views;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.ViewConversion
{
    public class UserViewConverter : BaseHistoricViewConverter, IViewConverter<UserViewFull, EntityPackage>
    {
        protected IPermissionService service;

        public UserViewConverter(IPermissionService service)
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

            ApplyToViewHistoric(user, result);

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

            var newUser = NewEntity(user.username)
                .Add(NewValue(Keys.AvatarKey, user.avatar.ToString()))
                .Add(NewValue(Keys.EmailKey, user.email))
                .Add(NewValue(Keys.PasswordSaltKey, user.salt))
                .Add(NewValue(Keys.PasswordHashKey, user.password));
            ApplyFromViewHistoric(user, newUser, Keys.UserType);
            //Can't do anything about super
            
            if(!string.IsNullOrWhiteSpace(user.registrationKey))
                newUser.Add(NewValue(Keys.RegistrationCodeKey, user.registrationKey));

            return newUser;
        }

    }
}