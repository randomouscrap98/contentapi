using System.Text.RegularExpressions;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected async Task ConvertUsers()
    {
        logger.LogTrace("ConvertUsers called");

        var oldUsers = new List<oldsbs.Users>();

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            //I'm assuming there's not enough users to matter, so we're not doing it in batches
            //Also, we are specifically excluding users who currently have a lockout or shadow ban. Although they may 
            //have content linked to them that won't show up appropriately, we'll simply remove any content
            //for which we don't have a linked user. We will absolutely log when that happens though
            oldUsers = (await oldcon.QueryAsync<oldsbs.Users>("select * from users")).ToList();
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
                var user = new Db.User_Convert()
                {
                    id = x.uid,
                    username = x.username,
                    email = x.email,
                    createDate = x.created,
                    avatar = x.avatar, //THIS IS TEMPORARY!! 
                    password = "",
                    salt = "",
                    special = "",
                    super = x.uid == config.SuperUserId,
                    deleted = deleteHash.Contains(x.uid),
                    lastPasswordDate = new DateTime(0), //This forces everyone's passwords to be reset
                };

                if(user.super)
                    logger.LogInformation($"User {user.username}({user.id}) is super!");

                return user;
            }).ToList();

            logger.LogInformation($"Translated (in-memory) all the users; super user uid was {config.SuperUserId}");

            await con.InsertAsync(newUsers, trans);
            logger.LogInformation($"Wrote {newUsers.Count()} users into contentapi!");
        });

        await AddUserPages(oldUsers);
    }

    protected async Task AddUserPages(List<oldsbs.Users> oldUsers)
    {
        logger.LogTrace($"AddUserPages called with {oldUsers.Count} users");
        var userpageParent = new Db.Content();

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            //Create yet another system page which is the parent of all userpages
            userpageParent = await AddSystemContent("userpages", con, trans, true);

            //After adding all users, we now take their abouts and turn them into real pages. We're going to let the API do the 
            //work so the pages are as normal as possible. The create date is of course incorrect but it doesn't matter too much,
            //as that date information isn't preserved in the old database anyway
            foreach(var oldUser in oldUsers)
            {
                if(!string.IsNullOrEmpty(oldUser.about))
                {
                    var content = new Db.Content
                    {
                        contentType = data.InternalContentType.userpage,
                        text = oldUser.about,
                        parentId = userpageParent.id,
                        createDate = oldUser.created, //This isn't correct but whatever
                        createUserId = oldUser.uid,
                        name = $"{oldUser.username}'s userpage"
                    };

                    content.hash = await GetTitleHash(content.name, con);

                    await AddGeneralPage(content, con, trans);
                }
                else
                {
                    logger.LogDebug($"No userpage for {oldUser.username}");
                }
            }
        });


        using(var con = services.dbFactory.CreateRaw())
        {
            var userpageCount = await con.ExecuteScalarAsync<int>("select count(*) from content where contentType = @type", new { type = data.InternalContentType.userpage });
            logger.LogInformation($"Inserted {userpageCount} userpages!");
        }
    }

    protected async Task ConvertUserSettings()
    {
        logger.LogTrace("ConvertUserSettings called");

        await PerformChunkedTransfer<oldsbs.Settings>("settings", "uid,name", async (oldcon, con, trans, oldSettings, start) =>
        {
            await con.InsertAsync(oldSettings.Select(x => new Db.UserVariable
            {
                userId = x.uid,
                key = "setting:" + x.name,
                value = x.value
            }), trans);
            logger.LogInformation($"Inserted {oldSettings.Count} settings (chunk {start})");
        });
        logger.LogInformation("Converted all user settings!");
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
            await con.UpdateAsync(users, trans);
            logger.LogInformation($"Updated all avatars for {users.Count()} users");
        });
    }
}