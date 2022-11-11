using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertBadgeGroups()
    {
        logger.LogTrace("ConvertBadgeGroups called");

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var oldGroups = await oldcon.QueryAsync<oldsbs.BadgeGroups>("select * from badgegroups");
            logger.LogInformation($"Found {oldGroups.Count()} badgegroups in old database");

            //Because this one needs values for each, we have to insert them individually? It doesn't 
            //matter because we're in a transaction
            foreach(var og in oldGroups)
            {
                var ng = await AddSystemContent(new Db.Content {
                    literalType = "badgegroup",
                    name = og.name,
                    description = og.description
                }, con, trans); 
                logger.LogDebug($"Wrote badgegroup {ng.name}({ng.id})");
                var values = new List<Db.ContentValue> { CreateValue(ng.id, "bgid", og.bgid) };
                if(og.single) values.Add(CreateValue(ng.id, "single", true));
                if(og.starter) values.Add(CreateValue(ng.id, "starter", true));
                await con.InsertAsync(values, trans);
                logger.LogDebug($"Inserted {values.Count} value(s) for badgegroup {ng.name}({ng.id})[{og.bgid}]");
            }

            logger.LogInformation($"Wrote {oldGroups.Count()} badgegroups into contentapi!");
        });
    }

    protected async Task ConvertBadges()
    {
        logger.LogTrace("ConvertBadges called");

        var oldBadges = new List<oldsbs.Badges>();
        var groupsForBadges = new List<oldsbs.GroupsForBadges>();
        var groupContentMapping = new Dictionary<long, long>();
        var badgeGroupMapping = new Dictionary<long, long>();

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            oldBadges = (await oldcon.QueryAsync<oldsbs.Badges>("select * from badges")).ToList();
            logger.LogInformation($"Found {oldBadges.Count()} badges in old database");

            groupsForBadges = (await oldcon.QueryAsync<oldsbs.GroupsForBadges>("select * from groupsforbadges")).ToList();
            logger.LogInformation($"Found {groupsForBadges.Count()} groupsforbadges in old database");

            badgeGroupMapping = groupsForBadges.ToDictionary(k => k.bid, v => v.bgid);

            var groupValues = await con.QueryAsync<Db.ContentValue>(
                @"select * from content_values 
                   where key=""bgid""
                     and contentId in (select id from content where literalType=""system:badgegroup"")");

            groupContentMapping = groupValues.ToDictionary(k => JsonConvert.DeserializeObject<long>(k.value), v => v.contentId);
            groupContentMapping.Add(0, 0); //Make sure "no group" matches to root
            logger.LogInformation("BGID to content mapping for badge insertion: " + 
                string.Join(" ", groupContentMapping.Select(x => $"({x.Key}={x.Value})")));
        });

        //Now we insert files outside the transaction... note that this IS inserting all the badges, since badges
        //are files with no additional data now. This is in contrast with users, where their avatar is a file but is also a field
        //associated with them
        foreach(var oldBadge in oldBadges)
        {
            var bgid = badgeGroupMapping.GetValueOrDefault(oldBadge.bid, 0); //ContainsKey(oldBadge.bid) ? badgeGroupMapping[oldBadge.bid]

            if(bgid == 0)
                logger.LogWarning($"Badge {oldBadge.name}({oldBadge.bid}) has no group mapping!");
            
            if(!groupContentMapping.ContainsKey(bgid))
                throw new InvalidOperationException($"Could not find appropriate content group for badge {oldBadge.name}({oldBadge.bid}), bgid = {bgid}");
            
            var parentId = groupContentMapping[bgid];

            using (var fstream = System.IO.File.Open(Path.Combine(config.BadgePath, oldBadge.file), FileMode.Open, FileAccess.Read))
            {
                //oops, we have to actually upload the file
                var fcontent = await fileService.UploadFile(new data.Views.ContentView()
                {
                    name = oldBadge.name,
                    contentType = data.InternalContentType.file, 
                    description = oldBadge.description,
                    parentId = parentId,
                    values = new Dictionary<string, object> {
                        { "system", true }, //We may want to skip system content in browsing
                        { "badge", true }, //Might need to know if a file is a badge. since files literaltypes are stuck with the mimetype...
                        { "bid", oldBadge.bid },        //The original bid (id of the badge)
                        { "bgid", oldBadge.bid },       //The original bgid
                        { "file", oldBadge.file },      //The original filename
                        { "value", oldBadge.value },    //Badges had a score but the system was never used properly
                        { "givable", oldBadge.givable }, //This badge WAS givable by admins at one point
                        { "hidden", oldBadge.hidden }, 
                        { "single", oldBadge.single } //Can only be given to a single person
                    },
                    permissions = new Dictionary<long, string> { { 0, "CR" }} //NEEDS to be public 
                }, new Main.UploadFileConfig() { }, fstream, config.SuperUserId);

                logger.LogDebug($"Uploaded badge {oldBadge.name}({oldBadge.bid}): {fcontent.name} ({fcontent.hash})");

            }
        }
    }
}