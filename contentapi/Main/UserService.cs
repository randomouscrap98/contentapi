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

    public async Task CheckValidUsernameAsync(string username, long existingUserId = 0)
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

    public async Task CheckValidEmailAsync(string email)
    {
        if(string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Must provide email to create new user!");

        var existing = await dbcon.ExecuteScalarAsync<int>($"select count(*) from {searcher.GetDatabaseForType<UserView>()} where email = @user", new { user = email });

        if(existing > 0)
            throw new ArgumentException($"Duplicate email in system: '{email}'");
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
    
    public void CheckValidUser(IDictionary<string, object> result)
    {
        if(!result.ContainsKey("type"))
            throw new InvalidOperationException("Tried to check for valid user without retrieving type!");

        if((long)result["type"] != (long)UserType.user)
            throw new ForbiddenException("Can only perform user session actions with real users!");
    }

    /// <summary>
    /// Item1 is salt, Item2 is password, both are ready to be stored in a user
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    public Tuple<string,string> GenerateNewPasswordData(string password)
    {
        var salt = hashService.GetSalt();
        return Tuple.Create(Convert.ToBase64String(salt), Convert.ToBase64String(hashService.GetHash(password, salt)));
    }

    public async Task<UserView> CreateNewUser(string username, string password, string email)
    {
        await CheckValidUsernameAsync(username);
        await CheckValidEmailAsync(email);
        CheckValidPassword(password);

        var user = new Db.User {
            username = username,
            email = email,
            createDate = DateTime.UtcNow,
            registrationKey = Guid.NewGuid().ToString(),
            type = UserType.user
        };

        var passwordData = GenerateNewPasswordData(password);
        user.salt = passwordData.Item1;
        user.password = passwordData.Item2;

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

    public async Task<long> GetUserIdFromEmailAsync(string email)
    {
        var user = (await searcher.QueryRawAsync(
            $"select id from {searcher.GetDatabaseForType<UserView>()} where email = @email",
            new Dictionary<string, object> { { "email", email}})).FirstOrDefault();
        
        if(user == null)
            throw new ArgumentException("Email not found!");
        
        return (long)user["id"];
    }

    public async Task<string> GetRegistrationKeyAsync(long userId)
    {
        //Go get the registration key
        var userRegistration = (await searcher.QueryRawAsync(
            $"select id, registrationKey, type from {searcher.GetDatabaseForType<UserView>()} where id = @user",
            new Dictionary<string, object> { { "user", userId}})).FirstOrDefault();

        if(userRegistration == null)
            throw new ArgumentException($"User {userId} not found!");

        CheckValidUser(userRegistration);

        return (string)userRegistration["registrationKey"];
    }

    public async Task<string> CompleteRegistration(long userId, string registrationKey)
    {
        string realKey = await GetRegistrationKeyAsync(userId); 

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

    protected async Task<string> LoginGeneric(string fieldname, object value, string password, TimeSpan? expireOverride)
    {
        //First, find the user they're even talking about. 

        //TODO: Consider making a special user view specifically for working with real user data, so we don't have to keep doing raw database
        //stuff when it comes to private user data. For instance, you have to check if "salt" is non-empty here, but that's very dependent on
        //how we mark if users are deleted or not. We ASSUME we don't want the salt (or the password) kept on deletion, BUT...

        //Next, get the LEGITIMATE data from the database
        var userSecrets = (await searcher.QueryRawAsync(
            $"select id, password, salt, registrationKey, type from {searcher.GetDatabaseForType<UserView>()} where {fieldname} = @user and deleted=0",
            new Dictionary<string, object> { { "user", value }})).FirstOrDefault();

        if(userSecrets == null)
            throw new ArgumentException("User not found!");

        CheckValidUser(userSecrets);
        
        //So for registration, it's SPECIFICALLY null which makes you registered! This is IMPORTANT!
        if(userSecrets["registrationKey"] != null)
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

    public Task<string> LoginUsernameAsync(string username, string password, TimeSpan? expireOverride = null)
    {
        return LoginGeneric("username", username, password, expireOverride);
    }

    public async Task VerifyPasswordAsync(long userId, string password)
    {
        //The login will tell us all the exceptions we need to know
        await LoginGeneric("id", userId, password, null);
    }

    public void InvalidateAllTokens(long userId)
    {
        authTokenService.InvalidateAllTokens(userId);
    }

    public async Task<UserGetPrivateData> GetPrivateData(long userId)
    {
        var result = new UserGetPrivateData();
        var queryResult = await searcher.QueryRawAsync($"select * from {searcher.GetDatabaseForType<UserView>()} where id = @id", 
            new Dictionary<string, object>() { { "id", userId }});
        var castResult = searcher.ToStronglyTyped<User>(queryResult);

        if(castResult.Count != 1)
            throw new ArgumentException($"Couldn't find user {userId}");

        //WARN: this technically lets you get private data for groups!
        
        //This could also be done with an auto-mapper but
        result.email = castResult.First().email;
        result.hideList = castResult.First().hideListParsed;

        return result;
    }

    public async Task SetPrivateData(long userId, UserSetPrivateData data)
    {
        var sets = new Dictionary<string,string>();

        //Go find the data to add
        if(data.password != null)
        {
            CheckValidPassword(data.password);
            var pwdata = GenerateNewPasswordData(data.password);
            sets.Add("salt", pwdata.Item1);
            sets.Add("password", pwdata.Item2);
        }

        if(data.email != null)
        {
            await CheckValidEmailAsync(data.email);
            sets.Add("email", data.email);
        }

        if(data.hideList != null) //TODO: maybe move the parsing/generation code for hidelists out of the user data object
            sets.Add("hidelist", User.HideListToString(data.hideList));

        //They didn't appear to add any data!
        if(sets.Count == 0)
            throw new ArgumentException($"No private data sent for user {userId}!");
        
        var parameters = new Dictionary<string, object> {{ "id" , userId }};

        foreach(var kp in sets)
            parameters.Add(kp.Key, kp.Value);
        
        //WARN: this technically lets you set private data for groups!

        var count = await dbcon.ExecuteAsync($"update users set {string.Join(",", sets.Select(x => $"{x.Key} = @{x.Key}"))} where id = @id", parameters);

        if(count != 1)
            throw new ArgumentException($"Couldn't find user {userId}");
    }
}