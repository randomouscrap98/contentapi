using System.Collections.Generic;

namespace contentapi.Services.Implementations
{
    public class UserValidationService
    {
        protected Dictionary<long, int> userValidation = new Dictionary<long, int>();
        protected readonly object validationLock = new object();

        public string GetUserValidationToken(long userId)
        {
            lock(validationLock)
            {
                int result = 0;

                if(userValidation.ContainsKey(userId))
                    result = userValidation[userId];
                
                return result.ToString();
            }
        }

        public void NewValidation(long userId)
        {
            lock(validationLock)
            {
                if(!userValidation.ContainsKey(userId))
                    userValidation.Add(userId, 0);
                
                userValidation[userId]++;
            }
        }
    }
}