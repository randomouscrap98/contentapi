using contentapi.Views;

namespace contentapi.Main;

public interface IUserService
{
    //Task<UserView?> GetByUsernameAsync(string username);
    //Task<UserView?> GetByEmailAsync(string email);

    void InvalidateAllTokens(long userId);
    Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null);
    Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride = null);
    Task<UserView> CreateNewUser(string username, string password, string email);
    Task<string> GetRegistrationKeyAsync(long userId);
    Task<string> CompleteRegistration(long userId, string registrationKey);
}