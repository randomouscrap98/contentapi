using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using AutoMapper;
using contentapi.Db;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using contentapi.Services.Constants;
using contentapi.Views;
using System.Collections.Generic;
using contentapi.Services;
using contentapi.Db.History;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace contentapi.Controllers
{
    public class NewConvertControllerConfig
    {
        public string SecretKey {get;set;}
    }

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
        protected ModuleMessageViewSource moduleMessageSource;
        protected ModuleRoomMessageViewSource moduleRMessageSource;
        protected  ActivityViewSource activitySource;
        //protected  ContentApiDbContext ctapiContext;
        protected IMapper mapper;
        protected IDbConnection newdb;
        protected IEntityProvider entityProvider;
        protected IHistoryService historyService;
        protected NewConvertControllerConfig config;
        protected IHistoryConverter historyConverter;


        public NewConvertController(ILogger<NewConvertController> logger, UserViewSource userSource, BanViewSource banSource,
            ModuleViewSource moduleViewSource, FileViewSource fileViewSource, ContentViewSource contentViewSource, 
            CategoryViewSource categoryViewSource, VoteViewSource voteViewSource, WatchViewSource watchViewSource, 
            CommentViewSource commentViewSource, ActivityViewSource activityViewSource, 
            ContentApiDbConnection cdbconnection, IEntityProvider entityProvider,
            ModuleMessageViewSource moduleMessageViewSource,
            ModuleRoomMessageViewSource moduleRoomMessageViewSource,
            NewConvertControllerConfig config,
            IHistoryService historyService,
            IHistoryConverter historyConverter,
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
            this.entityProvider = entityProvider;
            this.moduleMessageSource = moduleMessageViewSource;
            this.moduleRMessageSource = moduleRoomMessageViewSource;
            this.config = config;
            this.historyService = historyService;
            this.historyConverter = historyConverter;
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

        //Includes bans, uservariables
        [HttpGet("users")]
        public async Task<string> ConvertUsersAsync([FromQuery]string secret)
        {
            if(secret != config.SecretKey)
                throw new InvalidOperationException("Need the secret");

            newdb.Open();
            using (var trs = newdb.BeginTransaction())
            {
                try
                {
                    Log("Starting user convert");
                    var users = await userSource.SimpleSearchAsync(new UserSearch());
                    Log($"{users.Count} users found");
                    foreach (var user in users)
                    {
                        var newUser = mapper.Map<Db.User_Convert>(user);
                        //User dapper to store?
                        var id = await newdb.InsertAsync(newUser);
                        Log($"Inserted user {newUser.username}({id})");
                    }
                    var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM users");
                    Log($"Successfully inserted users, {count} in table");

                    Log("Starting ban convert");
                    var bans = await banSource.SimpleSearchAsync(new BanSearch());
                    Log($"{bans.Count} bans found");
                    foreach (var ban in bans)
                    {
                        var newban = mapper.Map<Db.Ban>(ban);
                        //User dapper to store?
                        var id = await newdb.InsertAsync(newban);
                        Log($"Inserted ban for {newban.bannedUserId}({id})");
                    }
                    count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM bans");
                    Log($"Successfully inserted bans, {count} in table");

                    Log("Starting user variable convert");
                    //var realKeys = keys.Select(x => Keys.VariableKey + x);

                    var evs = await entityProvider.GetQueryableAsync<EntityValue>();
                    var ens = await entityProvider.GetQueryableAsync<Entity>();

                    var query =
                        from v in evs
                        where EF.Functions.Like(v.key, $"{Keys.VariableKey}%")
                    //where EF.Functions.Like(v.key, Keys.VariableKey + key) && v.entityId == -uid
                    join e in ens on -v.entityId equals e.id
                        where EF.Functions.Like(e.type, $"{Keys.UserType}%")
                        select v;

                    var uvars = await query.ToListAsync();
                    Log($"{uvars.Count} user variables found");

                    foreach (var uvar in uvars)
                    {
                        var newvar = new UserVariable()
                        {
                            id = uvar.id,
                            userId = -uvar.entityId,
                            createDate = uvar.createDate ?? DateTime.Now,
                            editCount = 0,
                            key = uvar.key.Substring(Keys.VariableKey.Length),
                            value = uvar.value
                        };
                        newvar.editDate = newvar.createDate;
                        var id = await newdb.InsertAsync(newvar);
                        Log($"Inserted uservariable {newvar.key} for {newvar.userId}({id})");
                    }

                    count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM user_variables");
                    Log($"Successfully inserted user variables, {count} in table");
                    trs.Commit();
                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION: {ex}");
                    trs.Rollback();
                }
            }

            return DumpLog();
        }

        protected async Task<List<long>> ConvertCt<T>(Func<Task<List<T>>> producer, Func<long, Task<List<T>>> historyProducer, Func<Db.Content_Convert, T, Db.Content_Convert> modify = null) where T : StandardView
        {
            var ids = new List<long>();
            var tn = typeof(T);
            Log($"Starting {tn.Name} convert");
            var content = await producer();
            foreach (var ct in content)
            {
                var nc = mapper.Map<Db.Content_Convert>(ct);
                nc.deleted = false;
                if(modify != null)
                    nc = modify(nc, ct);
                var id = await newdb.InsertAsync(nc);
                Log($"Inserted {tn.Name} '{nc.name}'({id})");

                //Now grab the keywords and permissions and values
                var kws = ct.keywords.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new ContentKeyword()
                {
                    contentId = id,
                    value = x
                }).ToList();
                var lcnt = await newdb.InsertAsync(kws); //IDK if the list version has async
                Log($"Inserted {lcnt} keywords for '{nc.name}'");

                var vls = ct.values.Where(x => !String.IsNullOrEmpty(x.Value)).Select(x => new ContentValue()
                {
                    contentId = id,
                    key = x.Key,
                    value = JsonConvert.SerializeObject(
                        x.Key == "badsbs2" ? long.Parse(x.Value) : 
                        x.Key == "photos" ? (object)x.Value.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) : 
                        x.Key == "pinned" ? (object)x.Value.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(y => long.Parse(y)) : 
                        (object)x.Value)
                    //value = JsonConvert.SerializeObject(Regex.IsMatch(x.Value, @"^\s*(\s*\d+\s*,\s*)*\d+\s*$") ? 
                    //    (object)x.Value.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) : (object)x.Value)
                }).ToList();

                lcnt = await newdb.InsertAsync(vls); //IDK if the list version has async
                Log($"Inserted {lcnt} values for '{nc.name}'");

                //Remove createuser if there, then readd with full permissions
                var pms = ct.permissions.Where(x => x.Key != ct.createUserId).Select(x => new ContentPermission()
                {
                    contentId = id,
                    userId = x.Key,
                    create = x.Value.ToLower().Contains(Actions.KeyMap[Keys.CreateAction].ToLower()),
                    read = x.Value.ToLower().Contains(Actions.KeyMap[Keys.ReadAction].ToLower()),
                    update = x.Value.ToLower().Contains(Actions.KeyMap[Keys.UpdateAction].ToLower()),
                    delete= x.Value.ToLower().Contains(Actions.KeyMap[Keys.DeleteAction].ToLower())
                }).ToList();

                pms.Add(new ContentPermission {
                    contentId = id,
                    userId = ct.createUserId,
                    create = true, read = true, update = true, delete = true
                });

                lcnt = await newdb.InsertAsync(pms); //IDK if the list version has async
                Log($"Inserted {lcnt} permissions for '{nc.name}'");

                //This one is a bit tricky: get the history
                var revisions = await historyProducer(id); //contentSource.GetRevisions(id);
                var activity = await activitySource.SimpleSearchAsync(new ActivitySearch(){
                    ContentIds = new List<long>() { id },
                    Sort = "id"
                });
                //Just append ourselves to the end, that's how the history works in this system
                revisions.Add(ct);

                //If revisions and activity don't line up, we can't save the history
                if(revisions.Count != activity.Count)
                {
                    Log($"WARN: Couldn't produce history for {id}: {revisions.Count} revisions (plus current) but {activity.Count} activity");
                }
                else
                {
                    for(int i = 0; i < revisions.Count; i++)
                    {
                        var rv = mapper.Map<Db.Content_Convert>(revisions[i]);
                        var sn = mapper.Map<Db.History.ContentSnapshot>(rv);
                        sn.values = vls;
                        sn.permissions = pms;
                        sn.keywords = kws;
                        var action = UserAction.update;
                        switch(activity[i].action)
                        {
                            case "c" : case "!c": action = UserAction.create; break;
                            case "d" : case "!d": 
                                action = UserAction.delete; break;
                        };
                        var history = await historyConverter.ContentToHistoryAsync(sn, activity[i].userId, action, activity[i].date);
                        await newdb.InsertAsync(history);
                        Log($"Inserted history {history.createUserId}-{action}({activity[i].action}) for '{nc.name}'");
                    }
                }

                //And might as well go out and get the watches and votes, since i think
                //those COULD be tied to bad/old content... or something.
                //if(extra != null)
                //    await extra(id);
                ids.Add(id);
            }
            var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content");
            Log($"Successfully inserted {tn.Name}, {count} in table");
            return ids;
        }

        [HttpGet("content")]
        public async Task<string> ConvertContentAsync([FromQuery]string secret)
        {
            if(secret != config.SecretKey)
                throw new InvalidOperationException("Need the secret");
            newdb.Open();
            using (var trs = newdb.BeginTransaction())
            {
                try
                {
                    var ids = await ConvertCt(
                        () => contentSource.SimpleSearchAsync(new ContentSearch()),
                        (id) => contentSource.GetRevisions(id));

                    //Need to get votes and watches ONLY for real content
                    Log("Starting vote convert");
                    var votes = await voteSource.SimpleSearchAsync(new VoteSearch()
                    {
                        ContentIds = ids
                    });
                    Log($"{votes.Count} votes found");
                    foreach (var v in votes)
                    {
                        var newVote = mapper.Map<ContentVote>(v);
                        var vt = v.vote.ToLower();
                        if (vt == "b") newVote.vote = VoteType.bad;
                        if (vt == "o") newVote.vote = VoteType.ok;
                        if (vt == "g") newVote.vote = VoteType.good;
                        //User dapper to store?
                        var id = await newdb.InsertAsync(newVote);
                        Log($"Inserted vote {newVote.userId}-{newVote.contentId}({id})");
                    }
                    var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content_votes");
                    Log($"Successfully inserted votes, {count} in table");

                    Log("Starting watch convert");
                    var watches = await watchSource.SimpleSearchAsync(new WatchSearch()
                    {
                        ContentIds = ids
                    });
                    Log($"{watches.Count} watches found");
                    foreach (var w in watches)
                    {
                        var neww = mapper.Map<ContentWatch>(w);
                        //User dapper to store?
                        var id = await newdb.InsertAsync(neww);
                        Log($"Inserted watch {neww.userId}-{neww.contentId}({id})");
                    }
                    count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content_watches");
                    Log($"Successfully inserted watches, {count} in table");

                    await ConvertCt(
                        () => fileSource.SimpleSearchAsync(new FileSearch() { SearchAllBuckets = true }), 
                        (id) => fileSource.GetRevisions(id) ,
                        (n, o) =>
                        {
                            if(!string.IsNullOrEmpty(o.bucket))
                            {
                                //Remove ALL public permissions for files with a bucket set
                                o.permissions.Remove(0);
                                o.values.Add("bucket", o.bucket);
                            }
                            return n;
                        });
                    await ConvertCt(
                        () => categorySource.SimpleSearchAsync(new CategorySearch()), 
                        (id) => categorySource.GetRevisions(id),
                        (n, o) =>
                        {
                            o.values.Add("localSupers", string.Join(",", o.localSupers));
                            return n;
                        });
                    await ConvertCt(
                        () => moduleSource.SimpleSearchAsync(new ModuleSearch()), 
                        (id) => moduleSource.GetRevisions(id),
                        (n, o) =>
                        {
                            o.permissions.Add(0, "CR"); //Create lets people... comment on modules?? cool?
                            return n;
                        });
                    trs.Commit();
                }
                catch (Exception ex)
                {
                    trs.Rollback();
                    Log($"EXCEPTION: {ex}");
                }
            }

            return DumpLog();
        }

        //Includes bans, uservariables
        [HttpGet("messages")]
        public async Task<string> ConvertMessagesAsync([FromQuery]string secret)
        {
            if(secret != config.SecretKey)
                throw new InvalidOperationException("Need the secret");
            newdb.Open();
            using (var trs = newdb.BeginTransaction())
            {
                try
                {
                    Log("Starting modulemessage convert");
                    var mms = await moduleMessageSource.SimpleSearchAsync(new ModuleMessageViewSearch());
                    Log($"{mms.Count} module messages found");
                    var mmids = new List<long>();
                    foreach (var mm in mms)
                    {
                        var umm = mapper.Map<UnifiedModuleMessageView>(mm);
                        var nmm = mapper.Map<Db.Comment_Convert>(umm);
                        //User dapper to store?
                        mmids.Add(await newdb.InsertAsync(nmm));
                        if(mmids.Count >= 1000)
                        {
                            Log($"Inserted messages: {string.Join(",", mmids)}");
                            mmids.Clear();
                        }
                    }
                    var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM comments");
                    Log($"Successfully inserted modulemessages, {count} in table");

                    Log("Starting modulemessage2 convert");
                    var cms = await moduleRMessageSource.SimpleSearchAsync(new CommentSearch());
                    Log($"{cms.Count} module messages 2 found");
                    mmids = new List<long>();
                    foreach (var mm in cms)
                    {
                        var umm = mapper.Map<UnifiedModuleMessageView>(mm);
                        var nmm = mapper.Map<Db.Comment_Convert>(umm);
                        //User dapper to store?
                        mmids.Add(await newdb.InsertAsync(nmm));
                        if(mmids.Count >= 1000)
                        {
                            Log($"Inserted messages: {string.Join(",", mmids)}");
                            mmids.Clear();
                        }
                    }
                    count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM comments");
                    Log($"Successfully inserted modulemessages 2, {count} in table");

                    Log("Starting comment convert!!!");
                    var cmnts = await commentSource.SimpleSearchAsync(new CommentSearch());
                    Log($"{cmnts.Count} comments found");
                    var cmids = new List<long>();
                    foreach (var cmnt in cmnts)
                    {
                        var ncmnt = mapper.Map<Db.Comment_Convert>(cmnt);
                        //var cmtvals = new List<CommentValue>();
                        IDictionary<string, object> metaFields = null;
                        //Convert metadata into separate field

                        try
                        {
                            //string metadata = null;
                            var lines = ncmnt.text.Split("\n".ToCharArray());
                            metaFields = JsonConvert.DeserializeObject<Dictionary<string, object>>(lines[0]);

                            if(metaFields != null)
                            {
                                //WE ASSUME that if there are multiple lines, it's the new format
                                if(lines.Length > 1)
                                    ncmnt.text = ncmnt.text.Substring(lines[0].Length + 1); // +1 to skip newline
                                else 
                                    ncmnt.text = (string)metaFields["t"];
                            }
                            else
                            {
                                Log($"Meta parsed to null [{ncmnt.id}]? Lines[0]: '{lines[0]}', text: '{ncmnt.text}'");
                            }
                        }
                        catch
                        {
                            Log($"Old comment format assumed [{ncmnt.id}], no metadata: {ncmnt.text}");
                        }

                        //User dapper to store?
                        var cmid = await newdb.InsertAsync(ncmnt);
                        cmids.Add(cmid);

                        if(metaFields != null)
                        {
                            await newdb.InsertAsync(metaFields.Select(x => new CommentValue()
                            {
                                commentId = cmid, 
                                key = x.Key,
                                value = JsonConvert.SerializeObject(x.Value.ToString())
                            }));
                        }

                        if(cmids.Count >= 1000)
                        {
                            Log($"Inserted comments: {string.Join(",", cmids)}");
                            cmids.Clear();
                        }
                    }
                    count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM comments");
                    Log($"Successfully inserted comments!!!, {count} in table");

                    trs.Commit();
                }
                catch (Exception ex)
                {
                    trs.Rollback();
                    Log($"EXCEPTION: {ex}");
                }
            }
            return DumpLog();
        }


        [HttpGet("all")]
        public async Task<string> ConvertAll([FromQuery]string secret)
        {
            var sb = new StringBuilder();

            sb.AppendLine(await ConvertUsersAsync(secret));
            sb.AppendLine("---------------");
            sb.AppendLine(await ConvertContentAsync(secret));
            sb.AppendLine("---------------");
            sb.AppendLine(await ConvertMessagesAsync(secret));

            return sb.ToString();
        }
    }
}