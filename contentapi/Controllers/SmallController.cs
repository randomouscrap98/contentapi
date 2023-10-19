using contentapi.Main;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;
using CsvHelper;
using System.Globalization;
using System.Text;

namespace contentapi.Controllers;


public class SmallController : BaseController
{
    protected const string CSVMIME = "text/csv";
    protected const string PLAINMIME = "text/plain";
    protected const int DEFAULTPULL = 30;

    //protected ShortcutsService shortcuts;
    protected IUserService userService;
    protected IPermissionService permissions;

    public SmallController(BaseControllerServices services, IUserService userService, IPermissionService permissions) : base(services) 
    { 
        this.userService = userService;
        this.permissions = permissions;
    }

    protected string GenericStatus(ContentView? content, MessageView? message, UserView? currentUser)
    {
        StringBuilder sb = new();

        if(content != null)
        {
            if (permissions.CanUserStatic(new UserView { id = 0 }, UserAction.read, content.permissions))
            {
                sb.Append('R');
            }
            if (currentUser != null)
            {
                if (permissions.CanUserStatic(currentUser, UserAction.create, content.permissions))
                    sb.Append('P');
                if (content.createUserId == currentUser.id)
                    sb.Append('O');
            }
        }
        if(message != null)
        {
            if(message.edited)
                sb.Append('E');
            if(message.deleted)
                sb.Append('D');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Nearly all result sets from SmallController will be of this "record" type
    /// </summary>
    public class GenericMessageResult
    {
        public string? contentTitle;
        public string? username;
        public string? message;
        public string? datetime;
        public string? type;
        public string? state;
        public long? cid;
        public long? uid;
        public long? mid;
    }

    protected GenericMessageResult MakeGenericMessageResult(ContentView? content, MessageView? message, UserView? user, UserView? currentUser)
    {
        return new GenericMessageResult {
            contentTitle = content?.name,
            username = user?.username,
            message = message?.text,
            datetime = message != null ? Constants.ToCommonDateString(message.createDate) : null,
            type = message?.module,
            state = GenericStatus(content, message, currentUser),
            cid = content?.id,
            uid = user?.id,
            mid = message?.id
        };
    }

    /// <summary>
    /// Return a simple string as plaintext, catching exceptions and returning the appropriate error
    /// </summary>
    /// <param name="work"></param>
    /// <returns></returns>
    protected async Task<ActionResult> SmallTaskCatch(Func<Task<string>> work, string contentType = CSVMIME)
    {
        var result = await MatchExceptions(work);

        if(result.Value == null)
            return result.Result!;
        else 
            return File(System.Text.Encoding.UTF8.GetBytes(result.Value), contentType);
    }

    /// <summary>
    /// Return a list of items as a CSV
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="work"></param>
    /// <returns></returns>
    protected async Task<ActionResult> SmallTaskCatch<T>(Func<Task<List<T>>> work)
    {
        var result = await MatchExceptions(work);

        if(result.Value == null)
        {
            return result.Result!;
        }
        else 
        {
            var csvconfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) {
                HasHeaderRecord = false,
                MemberTypes = CsvHelper.Configuration.MemberTypes.Fields
            };
            using var mstream = new MemoryStream();
            using var writer = new StreamWriter(mstream);
            using var csv = new CsvWriter(writer, csvconfig);
            csv.WriteRecords(result.Value);
            await writer.FlushAsync();
            return File(mstream.ToArray(), CSVMIME);
        }
    }

    [HttpGet("login")]
    public Task<ActionResult> Login([FromQuery]string username, [FromQuery]string password, [FromQuery]long expireSeconds = 0)
    {
        return SmallTaskCatch(async () => 
        {
            RateLimit(RateLogin, username);
            TimeSpan? expireOverride = expireSeconds > 0 ? TimeSpan.FromSeconds(expireSeconds) : null;
            return await userService.LoginUsernameAsync(username, password, expireOverride);
        }, PLAINMIME);
    }

    [Authorize()]
    [HttpGet("me")]
    public Task<ActionResult> Me()
    {
        return SmallTaskCatch(async () => 
        {
            var user = await GetUserViewStrictAsync();
            return new List<(long, string)> {
                (user.id, user.username)
            };
        });
    }

    [HttpGet("search")]
    public Task<ActionResult> Search([FromQuery]string search)
    {
        return SmallTaskCatch(async () => 
        {
            UserView user;
            try { user = await GetUserViewStrictAsync(); }
            catch { user = new UserView() { id = 0, username = "DEFAULT"}; }

            //Construct a search 
            var result = await CachedSearcher.GetByField<ContentView>(RequestType.content, nameof(ContentView.name), search, "like");
            return result.Select(x => MakeGenericMessageResult(x, null, null, user)).ToList();
            //return result.Select(x => (x.id, x.name, GenericStatus(x, null, user))).ToList();
        });
    }

    [Authorize()]
    [HttpGet("post/{id}")]
    public Task<ActionResult> Post([FromRoute]long id, [FromQuery]string message, [FromQuery(Name = "values")]Dictionary<string, string>? values = null)
    {
        return SmallTaskCatch(async () => 
        {
            var user = await GetUserViewStrictAsync();
            var mv = new MessageView {
                text = message,
                contentId = id,
                values = values?.ToDictionary(k=>k.Key, v=>(object)v.Value) ?? new Dictionary<string, object>()
            };
            //This will use the cached writer but that's fine, this particular instance of the smallcontroller will NOT
            //live long, just like the cached values suggest
            var result = await WriteAsync(mv);
            var content = await CachedSearcher.GetById<ContentView>(result.contentId);

            return new List<GenericMessageResult> {
                MakeGenericMessageResult(content, result, user, user)
            };
        });
    }
}