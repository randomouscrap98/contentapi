using contentapi.Controllers;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.oldsbs;

public static class UserConvert
{
    public static Task ConvertUsers(this OldSbsConvertController controller)
    {
        controller.logger.LogTrace("ConvertUsers called");

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        return controller.PerformDbTransfer(async (oldcon, con, trans) =>
        {
            //First, delete the old users
            await con.ExecuteAsync("delete from users");
            controller.logger.LogInformation("Deleted all users from contentapi");

            //I'm assuming there's not enough users to matter, so we're not doing it in batches
            //Also, we are specifically excluding users who currently have a lockout or shadow ban. Although they may 
            //have content linked to them that won't show up appropriately, we'll simply remove any content
            //for which we don't have a linked user. We will absolutely log when that happens though
            var users = await con.QueryAsync<oldsbs.Users>("select * from users");
            controller.logger.LogInformation($"Found {users.Count()} users in old database");

            var ignoredUsers = await con.QueryAsync<oldsbs.Users>(
                @"select uid, username from users
                  where uid in (select uid from registrations) 
                    or uid in (select uid from bans where end > curdate() and (lockout=1 or shadow=1))");
            
            controller.logger.LogWarning($"The following {ignoredUsers.Count()} users are being marked deleted: " + 
                string.Join(", ", ignoredUsers.Select(x => $"{x.username}({x.uid})")));
            
            var deleteHash = new HashSet<long>(ignoredUsers.Select(x => x.uid));

            var newUsers = users.Select(x => 
            {
                return new Db.User()
                {
                    id = x.uid,
                    username = x.username,
                    email = x.email,
                    createDate = x.created,
                    password = "",
                    salt = "",
                    special = "",
                    super = false,
                    deleted = deleteHash.Contains(x.uid),
                    lastPasswordDate = new DateTime(0), //This forces everyone's passwords to be reset
                };
            });

            controller.logger.LogInformation($"Translated (in-memory) all the users");

            await con.InsertAsync(newUsers);
            controller.logger.LogInformation($"Wrote {newUsers.Count()} users into contentapi!");
        });
    }
}