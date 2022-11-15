using System.Data;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    //This function does a lot of work because it's a bit more difficult to link to page data, so it's easier to just do it all
    //for each page as its pulled
    protected async Task ConvertNotifications()
    {
        logger.LogTrace("ConvertNotifications called");

        //Need thread and page mapping
        var allPageMapping = new Dictionary<long, long>();
        foreach(var bp in config.BasePageTypes)
            foreach(var pm in await GetOldToNewMapping("pid", bp.Key))
                allPageMapping.Add(pm.Key, pm.Value);
        var threadMapping = await GetOldToNewMapping("ftid", "forumthread");
        var threadCategoryMapping = await GetOldToNewMapping("fcid", "forumcategory");

        int skippedImportant = 0;
        int skippedTrash = 0;
        long maxMessageId = 0;
        long maxActivityId = 0;

        using (var con = services.dbFactory.CreateRaw())
        {
            maxMessageId = await con.ExecuteScalarAsync<long>("select max(id) from messages");
            maxActivityId = await con.ExecuteScalarAsync<long>("select max(id) from content_history");
        }

        await PerformChunkedTransfer<oldsbs.Notifications>("notifications", "nid", async (oldcon, con, trans, oldNotifications, start) =>
        {
            foreach(var oldNotif in oldNotifications)
            {
                long? contentId = null;

                if(oldNotif.area == "page" && allPageMapping.ContainsKey(oldNotif.linkid))
                    contentId = allPageMapping[oldNotif.linkid];
                else if(oldNotif.area == "thread" && threadMapping.ContainsKey(oldNotif.linkid))
                    contentId = threadMapping[oldNotif.linkid];
                else if(oldNotif.area == "threadlist" && threadCategoryMapping.ContainsKey(oldNotif.linkid))
                    contentId = threadCategoryMapping[oldNotif.linkid];

                if(contentId == null)
                {
                    if(oldNotif.area == "page" || oldNotif.area == "thread" || oldNotif.area == "threadlist")
                    {
                        logger.LogWarning($"Couldn't find related content for notification {oldNotif.nid}({oldNotif.area}-{oldNotif.linkid}) for user {oldNotif.uid}");
                        skippedImportant++;
                    }
                    else
                    {
                        logger.LogDebug($"Unconverted notification {oldNotif.nid}({oldNotif.area}-{oldNotif.linkid}) for user {oldNotif.uid}");
                        skippedTrash++;
                    }
                }
                else
                {
                    var watch = new Db.ContentWatch
                    {
                        userId = oldNotif.uid,
                        contentId = contentId.Value,
                        lastCommentId = maxMessageId,
                        lastActivityId = maxActivityId,
                        createDate = DateTime.UtcNow
                    };

                    await con.InsertAsync(watch, trans);
                }
            }

            logger.LogInformation($"Inserted {oldNotifications.Count} notifictions (chunk {start})");
        });

        logger.LogInformation($"Converted all notifications! Skipped {skippedImportant} important, {skippedTrash} not important");
    }

    //Events are RELATED to notifications... right?
    protected async Task ConvertEvents()
    {
        logger.LogTrace("ConvertEvents called");

        await ConvertHistoryGeneral<oldsbs.Events>("events", "eid", (m, h) =>
        {
            m.createDate = h.time;
            m.createUserId = h.uid; //Permissions don't matter with direct insert
        });

        logger.LogInformation("Converted all events!");
    }

}
        
