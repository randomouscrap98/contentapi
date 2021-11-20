using System;
using System.Text;
using System.Threading.Tasks;
using contentapi.Db;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewConvertController : Controller
    {
        protected  ILogger logger;
        protected  UserViewSource userSource;
        protected  BanViewSource banSource;
        protected  ModuleViewSource moduleSource;
        protected  FileViewSource fileSource;
        protected  ContentViewSource contentSource;
        protected  CategoryViewSource categorySource;
        protected  VoteViewSource voteSource;
        protected  WatchViewSource watchSource;
        protected  CommentViewSource commentSource;
        protected  ActivityViewSource activitySource;
        protected  ContentApiDbContext ctapiContext;


        public NewConvertController(ILogger<NewConvertController> logger, UserViewSource userSource, BanViewSource banSource,
            ModuleViewSource moduleViewSource, FileViewSource fileViewSource, ContentViewSource contentViewSource, 
            CategoryViewSource categoryViewSource, VoteViewSource voteViewSource, WatchViewSource watchViewSource, 
            CommentViewSource commentViewSource, ActivityViewSource activityViewSource, 
            ContentApiDbContext ctapiContext)
        {
            this.logger = logger;
            this.userSource = userSource;
            this.banSource = banSource;
            this.moduleSource = moduleViewSource;
            this.fileSource = fileViewSource;
            this.contentSource = contentViewSource;
            this.categorySource = categoryViewSource;
            this.voteSource = voteViewSource;
            this.watchSource = watchViewSource;
            this.commentSource = commentViewSource;
            this.activitySource = activityViewSource;
            this.ctapiContext = ctapiContext;
        }


        protected StringBuilder sb = new StringBuilder();

        protected void Log(string message)
        {
            logger.LogInformation(message);
            sb.AppendLine(message);
        }

        protected string DumpLog()
        {
            var result = sb.ToString();
            sb.Clear();
            return result;
        }


        [HttpGet("users")]
        public async Task<string> ConvertUsersAsync()
        {
            try
            {
                Log("Starting user convert");
                var users = await userSource.SimpleSearchAsync(new UserSearch());
                Log($"{users.Count} users found");
                //Need to map userview to user database object, then store them. 
            }
            catch(Exception ex)
            {
                Log($"EXCEPTION: {ex}");
            }

            return DumpLog();
        }
    }
}