using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;
using contentapi.Db;
using contentapi.Search;
using contentapi.Security;
using contentapi.Utilities;
using contentapi.Views;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.Main;

public class UserServiceConfig
{
    public TimeSpan TokenExpireDefault {get;set;} = TimeSpan.FromDays(10);
    public string UsernameRegex {get;set;} = "[a-zA-Z0-9_]+";
    public int MinUsernameLength {get;set;} = 2;
    public int MaxUsernameLength {get;set;} = 10;
    public int MinPasswordLength {get;set;} = 8;
    public int MaxPasswordLength {get;set;} = 32;
}

public class UserService : IUserService
{
    protected ILogger logger;
    protected IGenericSearch searcher;
    protected IHashService hashService;
    protected IAuthTokenService<long> authTokenService;
    protected UserServiceConfig config;
    protected IDbConnection dbcon;

    public ConcurrentDictionary<long, string> RegistrationLog = new ConcurrentDictionary<long, string>();

    public UserService(ILogger<UserService> logger, IGenericSearch searcher, IHashService hashService, IAuthTokenService<long> authTokenService,
        UserServiceConfig config, ContentApiDbConnection wrapper)
    {
        this.logger = logger;
        this.searcher = searcher;
        this.hashService = hashService;
        this.authTokenService = authTokenService;
        this.config = config;
        this.dbcon = wrapper.Connection;

        this.dbcon.Open();
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

    //public async Task<UserView?> GetByEmailAsync(string email)
    //{
    //    //var request = GetBasicRequest("email = @email");
    //    //request.values["email"] = email;
    //    //var result = (await searcher.SearchUnrestricted(request)).data[BasicRequestName];
    //    var result = await searcher.GetByField<UserView>(RequestType.user, "email", email);
    //    if(result.Count < 1) return null;
    //    return result.First();
    //}

    //public async Task<UserView?> GetByUsernameAsync(string username)
    //{
    //    var result = await searcher.GetByField<UserView>(RequestType.user, "username", username);
    //    if(result.Count < 1) return null;
    //    return result.First();
    //}

    public async Task CheckValidUsernameAsync(string username)
    {
        if(string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username can't be null or whitespace only!");
        if(username.Length < config.MinUsernameLength)
            throw new ArgumentException($"Username too short! Min length: {config.MinUsernameLength}");
        if(username.Length > config.MaxUsernameLength)
            throw new ArgumentException($"Username too long! Max length: {config.MaxUsernameLength}");
        if(!Regex.IsMatch(username, config.UsernameRegex))
            throw new ArgumentException($"Username has invalid characters, must match regex: {config.UsernameRegex}");

        var existing = await dbcon.ExecuteScalarAsync<int>($"select count(*) from {searcher.GetDatabaseForType<UserView>()} where username = @user", new { user = username });
        if(existing > 0)
            throw new ArgumentException($"Username '{username}' already taken!");
    }

    public void CheckValidPassword(string password)
    {
        if(string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password can't be null or just whitespace!");
        if(password.Length < config.MinPasswordLength)
            throw new ArgumentException($"Password too short! Must be at least {config.MinPasswordLength} characters");
        if(password.Length > config.MaxPasswordLength)
            throw new ArgumentException($"Password too long! Must be at most {config.MaxPasswordLength} characters");
    }

    public async Task<UserView> CreateNewUser(string username, string password, string email)
    {
        await CheckValidUsernameAsync(username);
        CheckValidPassword(password);

        if(string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Must provide email to create new user!");

        var existing = await dbcon.ExecuteScalarAsync<int>($"select count(*) from {searcher.GetDatabaseForType<UserView>()} where email = @user", new { user = email });
        if(existing > 0)
            throw new ArgumentException($"Duplicate email in system: '{email}'");
        
        var salt = hashService.GetSalt();

        var user = new Db.User {
            username = username,
            email = email,
            salt = Convert.ToBase64String(salt),
            createDate = DateTime.UtcNow,
            registrationKey = Guid.NewGuid().ToString()
        };

        user.password = Convert.ToBase64String(hashService.GetHash(password, salt));

        long id;

        using(var tsx = dbcon.BeginTransaction())
        {
            id = await dbcon.InsertAsync(user, tsx);

            if(id <= 0) 
                throw new InvalidOperationException("For some reason, writing the user to the database failed!");

            tsx.Commit();
        }

        //This might (confusingly) throw a "not found" exception. Mmmm
        var userView = await searcher.GetById<UserView>(RequestType.user, id, true);

        RegistrationLog.TryAdd(user.id, user.registrationKey);

        return userView;
    }
    
    public string GetNewTokenForUser(long uid, TimeSpan? expireOverride = null)
    {
        var data = new Dictionary<string, string>();
        return authTokenService.GetNewToken(uid, data, expireOverride ?? config.TokenExpireDefault);
    }

    public async Task<string> CompleteRegistration(long userId, string registrationKey)
    {
        //Go get the registration key
        var userRegistration = (await searcher.QueryRawAsync(
            $"select id, registrationKey from {searcher.GetDatabaseForType<UserView>()} where id = @user",
            new Dictionary<string, object> { { "user", userId}})).FirstOrDefault();

        if(userRegistration == null)
            throw new ArgumentException($"User {userId} not found!");

        string realKey = (string)userRegistration["registrationKey"];

        if(string.IsNullOrWhiteSpace(realKey))
            throw new RequestException($"User {userId} seems to already be registered!");

        if(realKey != registrationKey)       
            throw new RequestException($"Invalid registration key!");
        
        var count = await dbcon.ExecuteAsync("update users set registrationKey = NULL where id = @id", new { id = userId });

        if(count <= 0)
            throw new InvalidOperationException("Couldn't update user record to complete registration!");

        RegistrationLog.TryRemove(userId, out _);
        return GetNewTokenForUser(userId);
   }

    protected async Task<string> LoginGeneric(string fieldname, string value, string password, TimeSpan? expireOverride)
    {
        //First, find the user they're even talking about. 

        //Next, get the LEGITIMATE data from the database
        var userSecrets = (await searcher.QueryRawAsync(
            $"select id, password, salt, registrationKey from {searcher.GetDatabaseForType<UserView>()} where {fieldname} = @user",
            new Dictionary<string, object> { { "user", value }})).FirstOrDefault();

        if(userSecrets == null)
            throw new ArgumentException("User not found!");
        
        if(!string.IsNullOrWhiteSpace((string)userSecrets["registrationKey"]))
            throw new ForbiddenException("User not registered! Can't log in!");

        //Finally, compare hashes and if good, send out the token
        if(!hashService.VerifyText(password, Convert.FromBase64String((string)userSecrets["password"]), Convert.FromBase64String((string)userSecrets["salt"])))
            throw new RequestException("Password incorrect!");
        
        //Right now, I don't really have anything that needs to go in here
        return GetNewTokenForUser ((long)userSecrets["id"], expireOverride);
    }

    public Task<string> LoginEmailAsync(string email, string password, TimeSpan? expireOverride = null)
    {
        return LoginGeneric("email", email, password, expireOverride);
    }

    public void InvalidateAllTokens(long userId)
    {
        authTokenService.InvalidateAllTokens(userId);
    }

    public Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null)
    {
        return LoginGeneric("username", username, password, expireOverride);
    }
}