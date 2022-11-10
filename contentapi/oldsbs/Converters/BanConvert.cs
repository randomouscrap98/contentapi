using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertBans()
    {
        logger.LogTrace("Convertbans called");

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var oldBans = await oldcon.QueryAsync<oldsbs.Bans>("select * from bans");
            logger.LogInformation($"Found {oldBans.Count()} bans in old database");

            var newBans = oldBans.Select(x => 
            {
                return new Db.Ban()
                {
                    //Because the old system kept track of ban dates using activity, we don't have the bans here.
                    //I don't think it's worth it to link the bans because we'll have the archived activity anyway...
                    createDate = new DateTime(2015, 1, 1), 
                    expireDate = x.end,
                    //Again, the banner information is out with the activity, and so is lost
                    createUserId = 0,
                    bannedUserId = x.uid,
                    message = (x.reason ?? "") + (x.lockout ? " (WASLOCKOUT)" : "") + (x.shadow ? " (WASSHADOW)" : ""),
                    type = x.site ? (data.BanType.@public) : data.BanType.none
                };
            });

            logger.LogInformation($"Translated (in-memory) all the bans");

            await con.InsertAsync(newBans, trans);
            logger.LogInformation($"Wrote {newBans.Count()} bans into contentapi!");
        });
    }
}