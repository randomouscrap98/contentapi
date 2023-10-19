using contentapi.Db;
using contentapi.Main;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;
using CsvHelper;
using System.Globalization;

namespace contentapi.Controllers;


public class SmallController : BaseController
{
    protected const string CSVMIME = "text/csv";
    protected const string PLAINMIME = "text/plain";

    //protected ShortcutsService shortcuts;
    protected IUserService userService;

    public SmallController(BaseControllerServices services, IUserService userService) : base(services) 
    { 
        this.userService = userService;
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

}