using System.Data;
using contentapi.oldsbs;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace contentapi.Controllers;

public class OldSbsConvertControllerConfig
{
    public string OldSbsConnectionString {get;set;} = "";
    public string AvatarPath {get;set;} = "";
}

public class OldSbsConvertController : BaseController
{
    public OldSbsConvertControllerConfig config;
    public ILogger logger => services.logger;

    public OldSbsConvertController(BaseControllerServices services, OldSbsConvertControllerConfig config) : base(services)
    {
        this.config = config;
    }

    public IDbConnection GetOldSbsConnection()
    {
        return new MySqlConnection(config.OldSbsConnectionString);
    }

    /// <summary>
    /// Basically all transfer functions will follow this same pattern, so might as well just give it to them.
    /// </summary>
    /// <param name="transferFunc"></param>
    /// <returns></returns>
    public async Task PerformDbTransfer(Func<IDbConnection, IDbConnection, IDbTransaction, Task> transferFunc)
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
        logger.LogTrace("ConvertAll called! Each table will be emptied beforehand!");

        await this.ConvertUsers();
    }
}