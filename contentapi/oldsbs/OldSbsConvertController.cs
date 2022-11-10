using System.Data;
using contentapi.Controllers;
using contentapi.Main;
using contentapi.Search;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace contentapi.oldsbs;

public class OldSbsConvertControllerConfig
{
    public string OldSbsConnectionString {get;set;} = "";
    public string AvatarPath {get;set;} = "";
    public string OldDefaultAvatarRegex {get;set;} = "";
    public long PageIdSkip {get;set;}
    public long ThreadIdSkip {get;set;}
    public long ThreadCategoryIdSkip {get;set;}
    public long PageCategoryIdSkip {get;set;}
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

    [HttpGet()]
    public async Task ConvertAll()
    {
        logger.LogTrace("ConvertAll called! It expects an empty database and upload folder!");

        //NOTE: all these conversion functions are put into separate files because they're so big
        await ConvertUsers();
        await ConvertBans();
        await ConvertStoredValues();

        await UploadAvatars(); //This should come AFTER pages and threads and categories oh my, so 

        logger.LogInformation("Convert all complete?");
    }
}