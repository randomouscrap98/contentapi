using System.Data;
using contentapi.Controllers;
using contentapi.Main;
using contentapi.Search;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public class OldSbsConvertControllerConfig
{
    public string OldSbsConnectionString {get;set;} = "";
    public string AvatarPath {get;set;} = "";
    public string BadgePath {get;set;} = "";
    public string OldDefaultAvatarRegex {get;set;} = "";
    public long SuperUserId {get;set;}
    public long ContentIdSkip {get;set;}
    public long MessageIdSkip {get;set;}
    public int MaxChunk {get;set;} = 1000;
}

public partial class OldSbsConvertController : BaseController
{
    protected OldSbsConvertControllerConfig config;
    protected ILogger logger => services.logger;
    protected IFileService fileService;
    protected IGenericSearch searcher;
    protected IDbWriter writer;
    private static long nextId = 0;

    public OldSbsConvertController(BaseControllerServices services, OldSbsConvertControllerConfig config,
        IFileService fileService) : base(services)
    {
        this.config = config;
        this.fileService = fileService;
        this.searcher = services.dbFactory.CreateSearch();
        this.writer = services.dbFactory.CreateWriter();
    }

    protected IDbConnection GetOldSbsConnection()
    {
        return new MySqlConnection(config.OldSbsConnectionString);
    }

    protected string GetNextHash()
    {
        var id = Interlocked.Increment(ref nextId);
        return $"sbsconvert-{id}";
    }

    protected Task<Db.Content> AddSystemContent(string type, IDbConnection con, IDbTransaction trans, bool globalCreate = false)
    {
        return AddSystemContent(new Db.Content {
            literalType = type,
        }, con, trans, globalCreate);
    }

    /// <summary>
    /// System content is anything which may house data but which has no owner, only global read permission, no parent, and some special
    /// values to filter them out. They HAVE to have global read in order for them to be found AT ALL in the api
    /// </summary>
    /// <param name="whatfor"></param>
    /// <returns></returns>
    protected async Task<Db.Content> AddSystemContent(Db.Content content, IDbConnection con, IDbTransaction trans, bool globalCreate = false)
    {
        //Preset certain fields regardless of what was given
        content.createUserId = config.SuperUserId; //NOTE: it was changed such that most system content needs to be owned by the super user just in case.
        content.createDate = DateTime.UtcNow;
        content.deleted = false;
        content.hash = GetNextHash();
        content.contentType = data.InternalContentType.system;

        var id = await con.InsertAsync(content, trans);
        content.id = id;

        //And now we have to add the permission and the value
        await con.InsertAsync(globalCreate ? CreateBasicGlobalPermission(id) : CreateReadonlyGlobalPermission(id), trans);
        await con.InsertAsync(CreateSelfPermission(id, content.createUserId), trans);

        //WARN: DON'T use values to indicate system, even if system files might need that! The system content might
        //require those fields, and it's harder to filter out! 

        logger.LogInformation($"Inserted system content {CSTR(content)}");

        return content;
    }

    /// <summary>
    /// MOST user generated content should suffice with this. You should set the fields you care about; we ONLY set the hash and contentType
    /// </summary>
    /// <param name="content"></param>
    /// <param name="con"></param>
    /// <param name="trans"></param>
    /// <returns></returns>
    protected async Task<Db.Content> AddGeneralPage(Db.Content content, IDbConnection con, IDbTransaction trans, bool isReadonly = false, bool setBbcode = true)
    {
        //Assume the other fields are as people want them
        content.hash = GetNextHash();

        if(content.contentType == data.InternalContentType.none)
            content.contentType = data.InternalContentType.page;

        var id = await con.InsertAsync(content, trans);
        content.id = id;

        await con.InsertAsync(isReadonly ? CreateReadonlyGlobalPermission(id) : CreateBasicGlobalPermission(id));
        await con.InsertAsync(CreateSelfPermission(id, content.createUserId), trans);

        if(setBbcode)
            await con.InsertAsync(CreateValue(id, "markup", "bbcode"));

        logger.LogInformation($"Inserted general page {CSTR(content)}");

        return content;
    }

    //protected async Task<Db.Message> AddLazyConversion(object row, IDbConnection con, IDbTransaction trans)
    //{
    //    var message = new Db.Message()
    //    {
    //        creat
    //    }
    //}

    protected Db.ContentValue CreateValue(long contentId, string key, object? value) => new Db.ContentValue {
        contentId = contentId,
        key = key,
        value = JsonConvert.SerializeObject(value)
    };

    protected Db.MessageValue CreateMValue(long messageId, string key, object? value) => new Db.MessageValue {
        messageId = messageId,
        key = key,
        value = JsonConvert.SerializeObject(value)
    };

    protected Db.ContentPermission CreateReadonlyGlobalPermission(long contentId) => new Db.ContentPermission {
        contentId = contentId,
        userId = 0,
        read = true 
    };

    protected Db.ContentPermission CreateBasicGlobalPermission(long contentId) => new Db.ContentPermission {
        contentId = contentId,
        userId = 0,
        create = true,
        read = true 
    };

    protected Db.ContentPermission CreateSelfPermission(long contentId, long userId) => new Db.ContentPermission {
        contentId = contentId,
        userId = userId,
        create = true,
        read = true,
        update = true,
        delete = true
    };

    /// <summary>
    /// Assuming you saved the old id as a value, and the thing you're looking at has a unique type, this will return
    /// a simple mapping from old id to new id
    /// </summary>
    /// <param name="key"></param>
    /// <param name="type"></param>
    /// <param name="con"></param>
    /// <returns></returns>
    protected async Task<Dictionary<long, long>> GetOldToNewMapping(string key, string type, IDbConnection? con = null)
    {
        IDbConnection realCon;
        bool created = false;

        if(con == null)
        {
            realCon = services.dbFactory.CreateRaw();
            created = true;
        }
        else
        {
            realCon = con;
        }

        try
        {
            var values = await realCon.QueryAsync<Db.ContentValue>(
                @$"select * from content_values 
                where key=""{key}""
                    and contentId in (select id from content where literalType=""{type}"")");

            var result = values.ToDictionary(k => JsonConvert.DeserializeObject<long>(k.value), v => v.contentId);

            logger.LogInformation($"{key} to content mapping for {type}: " +
                string.Join(" ", result.Select(x => $"({x.Key}={x.Value})")));

            return result;
        }
        finally
        {
            if(created)
                realCon.Dispose();
        }

    }

    //protected void AddBasicMetadata(data.Views.ContentView content)
    //{
    //    content.values.Add("markup", "bbcode");
    //    content.permissions[0] = "CR";
    //}


    /// <summary>
    /// Basically all transfer functions will follow this same pattern, so might as well just give it to them.
    /// </summary>
    /// <param name="transferFunc"></param>
    /// <returns></returns>
    protected async Task PerformDbTransfer(Func<IDbConnection, IDbConnection, IDbTransaction, Task> transferFunc)
    {
        logger.LogTrace("PerformDbTransfer called");

        using(var oldcon = GetOldSbsConnection())
        {
            oldcon.Open();
            logger.LogDebug("Opened oldsbs connection!");

            using(var con = services.dbFactory.CreateRaw())
            {
                con.Open();
                logger.LogDebug("Opened contentapi connection!");

                using(var trans = con.BeginTransaction())
                {
                    await transferFunc(oldcon, con, trans);
                    trans.Commit();
                }
            }
        }
    }

    /// <summary>
    /// Too many things to operate on all at once? use this function to auto-chunk it all
    /// </summary>
    /// <param name="database"></param>
    /// <param name="order"></param>
    /// <param name="transferFunc"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected Task PerformChunkedTransfer<T>(string database, string order, Func<IDbConnection, IDbConnection, IDbTransaction, List<T>, int, Task> transferFunc)
    {
        logger.LogTrace($"PerformChunkedTransfer called for database {database}");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var count = await oldcon.ExecuteScalarAsync<int>($"select count(*) from {database}");
            logger.LogInformation($"Found {count} items in {database}; working in chunks of {config.MaxChunk}");

            for(var start = 0; start < count; start+=config.MaxChunk)
            {
                var oldStuff = (await oldcon.QueryAsync<T>($"select * from {database} order by {order} LIMIT {config.MaxChunk} OFFSET {start}")).ToList();
                await transferFunc(oldcon, con, trans, oldStuff, start);
            }
        });
    }

    protected Task SkipIds()
    {
        logger.LogTrace("SkipIds called");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            await con.InsertAsync(new Db.Content_Convert { id = config.ContentIdSkip, hash = "delete-later" }, trans);
            await con.InsertAsync(new Db.Message_Convert { id = config.MessageIdSkip }, trans);
            logger.LogInformation($"Inserted skip markers for content at {config.ContentIdSkip} and messages at {config.MessageIdSkip}");
        });
    }

    protected Task RemoveSkipMarkers()
    {
        logger.LogTrace("RemoveSkipMarkerscalled");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            await con.DeleteAsync(new Db.Content_Convert { id = config.ContentIdSkip }, trans);
            await con.DeleteAsync(new Db.Message_Convert { id = config.MessageIdSkip}, trans);
            logger.LogInformation($"Removed skip markers for content at {config.ContentIdSkip} and messages at {config.MessageIdSkip}");
        });
    }

    protected Task SanityChecks()
    {
        logger.LogTrace("SanityChecks called");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var badContentCount = await con.ExecuteScalarAsync<int>("select count(*) from content where id < @id", new { id = config.ContentIdSkip });
            var badMessageCount = await con.ExecuteScalarAsync<int>("select count(*) from messages where id < @id", new { id = config.MessageIdSkip });

            logger.LogInformation($"Sanity checks passed!");
        });
    }

    protected string CSTR(Db.Content content) => $"'{content.name}'/{content.hash}({content.literalType})[{content.id}]";
    protected string CSTR(data.Views.ContentView content) => $"'{content.name}'/{content.hash}({content.literalType})[{content.id}]";

    [HttpGet()]
    public async Task ConvertAll()
    {
        logger.LogTrace("ConvertAll called! It expects an empty database and upload folder!");

        await SkipIds();

        //NOTE: all these conversion functions are put into separate files because they're so big
        await ConvertUsers();
        await ConvertBans();
        await UploadAvatars(); //Because of our skip system, the ids for avatars no longer matter. To make things look nice, they should still come after content though
        await ConvertStoredValues();
        await ConvertBadgeGroups();
        await ConvertBadges();
        await ConvertForumCategories();
        await ConvertForumThreads();
        await ConvertForumPosts();
        await ConvertForumHistory();


        // Need to keep the skip markers around long enough to insert content with new ids
        await RemoveSkipMarkers();

        await SanityChecks();

        logger.LogInformation("Convert all complete?");
    }
}