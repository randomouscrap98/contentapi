using contentapi.Views;

namespace contentapi.Main;

public interface IUserService
{
    void InvalidateAllTokens(long userId);
    Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null);
    Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride = null);
    Task<long> CreateNewUser(string username, string password, string email);

    Task<string> GetRegistrationKeyAsync(long userId);

    /// <summary>
    /// Throw exceptions on username validity errors!
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    Task CheckValidUsernameAsync(string username, long existing = 0);

    /// <summary>
    /// Throws an exception with more information if the given password does not work for the given user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    Task VerifyPasswordAsync(long userId, string password);

    /// <summary>
    /// Since email is not publicly searchable, this is how you currently do it.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    Task<long> GetUserIdFromEmailAsync(string email);

    Task<string> CompleteRegistration(long userId, string registrationKey);

    Task<UserGetPrivateData> GetPrivateData(long userId);
    Task SetPrivateData(long userId, UserSetPrivateData data);
}