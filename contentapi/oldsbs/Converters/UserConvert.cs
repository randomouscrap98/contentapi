using System.Text.RegularExpressions;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertUsers()
    {
        logger.LogTrace("ConvertUsers called");

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            //I'm assuming there's not enough users to matter, so we're not doing it in batches
            //Also, we are specifically excluding users who currently have a lockout or shadow ban. Although they may 
            //have content linked to them that won't show up appropriately, we'll simply remove any content
            //for which we don't have a linked user. We will absolutely log when that happens though
            var oldUsers = await oldcon.QueryAsync<oldsbs.Users>("select * from users");
            logger.LogInformation($"Found {oldUsers.Count()} users in old database");

            var oldIgnoredUsers = await oldcon.QueryAsync<oldsbs.Users>(
                @"select uid, username from users
                  where uid in (select uid from registrations) 
                    or uid in (select uid from bans where end > curdate() and (lockout=1 or shadow=1))");
            
            logger.LogWarning($"The following {oldIgnoredUsers.Count()} users are being marked deleted: " + 
                string.Join(", ", oldIgnoredUsers.Select(x => $"{x.username}({x.uid})")));
            
            var deleteHash = new HashSet<long>(oldIgnoredUsers.Select(x => x.uid));

            var newUsers = oldUsers.Select(x => 
            {
                return new Db.User()
                {
                    id = x.uid,
                    username = x.username,
                    email = x.email,
                    createDate = x.created,
                    avatar = x.avatar, //THIS IS TEMPORARY!! 
                    password = "",
                    salt = "",
                    special = "",
                    super = false,
                    deleted = deleteHash.Contains(x.uid),
                    lastPasswordDate = new DateTime(0), //This forces everyone's passwords to be reset
                };
            });

            logger.LogInformation($"Translated (in-memory) all the users");

            await con.InsertAsync(newUsers, trans);
            logger.LogInformation($"Wrote {newUsers.Count()} users into contentapi!");
        });
    }

    /// <summary>
    /// Users must ALREADY be inserted!
    /// </summary>
    /// <returns></returns>
    protected async Task UploadAvatars()
    {
        logger.LogTrace("UploadAvatars called");

        List<Db.User> users = new List<Db.User>();

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            users = (await con.QueryAsync<Db.User>("select * from users")).ToList();
            logger.LogDebug($"Found {users.Count()} users to update avatars");
        });

        foreach(var user in users)
        {
            //Simple case: just use the default avatar (no upload required)
            if(Regex.IsMatch(user.avatar, config.OldDefaultAvatarRegex))
            {
                user.avatar = "0";
                logger.LogDebug($"Skipping default avatar for {user.username}({user.id})");
            }
            else
            {
                try
                {
                    using (var fstream = System.IO.File.Open(Path.Combine(config.AvatarPath, user.avatar), FileMode.Open, FileAccess.Read))
                    {
                        //oops, we have to actually upload the file
                        var fcontent = await fileService.UploadFile(new UploadFileConfigExtra()
                        {
                            name = user.avatar
                        }, fstream, user.id);

                        logger.LogDebug($"Uploaded avatar for {user.username}({user.id}): {fcontent.name} ({fcontent.hash})");

                        user.avatar = fcontent.hash;
                    }
                }
                catch(Exception ex)
                {
                    logger.LogWarning($"Couldn't import avatar for user {user.username}({user.id}), using default: {ex}");
                    user.avatar = "0";
                }
            }
        }

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            await con.InsertAsync(users, trans);
            logger.LogInformation($"Updated all avatars for {users.Count()} users");
        });
    }
}