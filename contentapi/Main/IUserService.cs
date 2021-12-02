using contentapi.Views;

namespace contentapi.Main;

public interface IUserService
{
    //Task<UserView?> GetByUsernameAsync(string username);
    //Task<UserView?> GetByEmailAsync(string email);

    Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride);
    Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride);
}