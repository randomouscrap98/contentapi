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
    //public long PageIdSkip {get;set;}
    //public long ThreadIdSkip {get;set;}
    //public long ThreadCategoryIdSkip {get;set;}
    //public long PageCategoryIdSkip {get;set;}
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

    protected Db.Content GetSystemContent(string whatfor)
    {
        return new Db.Content()
        {
            deleted = false,
            contentType = data.InternalContentType.none,
            createUserId = 0,
            parentId = 0,
            literalType = "system:" + whatfor,
            hash = GetNextHash(),
        };
    }

    protected Db.ContentValue CreateValue(long contentId, string key, object? value)
    {
        return new Db.ContentValue
        {
            contentId = contentId,
            key = key,
            value = JsonConvert.SerializeObject(value)
        };
    }


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

    [HttpGet()]
    public async Task ConvertAll()
    {
        logger.LogTrace("ConvertAll called! It expects an empty database and upload folder!");

        await SkipIds();

        //NOTE: all these conversion functions are put into separate files because they're so big
        await ConvertUsers();
        await ConvertBans();
        await ConvertStoredValues();
        await ConvertBadgeGroups();
        await ConvertBadges();

        await UploadAvatars(); //Because of our skip system, the ids for avatars no longer matter. To make things look nice, they should still come after content though

        // Need to keep the skip markers around long enough to insert content with new ids
        await RemoveSkipMarkers();

        await SanityChecks();

        logger.LogInformation("Convert all complete?");
    }
}