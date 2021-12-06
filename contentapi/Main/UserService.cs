using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.Views;

namespace contentapi.Main;

public class UserServiceConfig
{
    public TimeSpan TokenExpireDefault {get;set;} = TimeSpan.FromDays(10);
}

public class UserService : IUserService
{
    protected ILogger logger;
    protected IGenericSearch searcher;
    protected IHashService hashService;
    protected IAuthTokenService<long> authTokenService;
    protected UserServiceConfig config;

    public UserService(ILogger<UserService> logger, IGenericSearch searcher, IHashService hashService, IAuthTokenService<long> authTokenService,
        UserServiceConfig config)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.hashService = hashService;
        this.authTokenService = authTokenService;
        this.config = config;
    }

    public const string BasicRequestName = "userservicesearch";

    //protected SearchRequests GetBasicRequest(string query = "", string fields = "*")
    //{
    //    var request = new SearchRequests();
    //    request.requests.Add(new SearchRequest()
    //    {
    //        name = BasicRequestName,
    //        type = RequestType.user.ToString(),
    //        fields = fields,
    //        query = query
    //    });
    //    return request;
    //}

    public async Task<UserView?> GetByEmailAsync(string email)
    {
        //var request = GetBasicRequest("email = @email");
        //request.values["email"] = email;
        //var result = (await searcher.SearchUnrestricted(request)).data[BasicRequestName];
        var result = await searcher.GetByField<UserView>(RequestType.user, "email", email);
        if(result.Count < 1) return null;
        return result.First();
    }

    public async Task<UserView?> GetByUsernameAsync(string username)
    {
        var result = await searcher.GetByField<UserView>(RequestType.user, "username", username);
        if(result.Count < 1) return null;
        return result.First();
    }

    protected async Task<string> LoginGeneric(string fieldname, string value, string password, TimeSpan? expireOverride)
    {
        //First, find the user they're even talking about. 

        //Next, get the LEGITIMATE data from the database
        var userSecrets = (await searcher.QueryRawAsync(
            $"select id, password, salt from {searcher.GetDatabaseForType<UserView>()} where {fieldname} = @user",
            new Dictionary<string, object> { { "user", value }})).FirstOrDefault();

        if(userSecrets == null)
            throw new ArgumentException("User not found!");

        //Finally, compare hashes and if good, send out the token
        if(!hashService.VerifyText(password, Convert.FromBase64String((string)userSecrets["password"]), Convert.FromBase64String((string)userSecrets["salt"])))
            throw new RequestException("Password incorrect!");
        
        //Right now, I don't really have anything that needs to go in here
        var data = new Dictionary<string, string>();
        return authTokenService.GetNewToken((long)userSecrets["id"], data, expireOverride ?? config.TokenExpireDefault);
    }

    public Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride)
    {
        return LoginGeneric("email", email, password, expireOverride);
    }

    public void InvalidateAllTokens(long userId)
    {
        authTokenService.InvalidateAllTokens(userId);
    }

    public Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride)
    {
        return LoginGeneric("username", username, password, expireOverride);
    }
}