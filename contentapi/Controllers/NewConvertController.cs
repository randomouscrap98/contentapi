using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Dapper;
using Dapper.Contrib.Extensions;
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
        //protected  ContentApiDbContext ctapiContext;
        protected IMapper mapper;
        protected IDbConnection newdb;


        public NewConvertController(ILogger<NewConvertController> logger, UserViewSource userSource, BanViewSource banSource,
            ModuleViewSource moduleViewSource, FileViewSource fileViewSource, ContentViewSource contentViewSource, 
            CategoryViewSource categoryViewSource, VoteViewSource voteViewSource, WatchViewSource watchViewSource, 
            CommentViewSource commentViewSource, ActivityViewSource activityViewSource, 
            ContentApiDbConnection cdbconnection,
            /*ContentApiDbContext ctapiContext,*/ IMapper mapper)
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
            //this.ctapiContext = ctapiContext;
            this.mapper = mapper;
            this.newdb = cdbconnection.Connection;
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

        //Includes bans
        [HttpGet("users")]
        public async Task<string> ConvertUsersAsync()
        {
            try
            {
                Log("Starting user convert");
                var users = await userSource.SimpleSearchAsync(new UserSearch());
                Log($"{users.Count} users found");
                foreach(var user in users)
                {
                    var newUser = mapper.Map<Db.User>(user);
                    //User dapper to store?
                    var id = await newdb.InsertAsync(newUser);
                    Log($"Inserted user {newUser.username}({id})");
                }
                var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM users");
                Log($"Successfully inserted users, {count} in table");

                Log("Starting ban convert");
                var bans = await banSource.SimpleSearchAsync(new BanSearch());
                Log($"{bans.Count} bans found");
                foreach(var ban in bans)
                {
                    var newban = mapper.Map<Db.Ban>(ban);
                    //User dapper to store?
                    var id = await newdb.InsertAsync(newban);
                    Log($"Inserted ban for {newban.bannedUserId}({id})");
                }
                count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM bans");
                Log($"Successfully inserted bans, {count} in table");
            }
            catch(Exception ex)
            {
                Log($"EXCEPTION: {ex}");
            }

            return DumpLog();
        }
    }
}