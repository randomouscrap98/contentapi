
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertForumCategories()
    {
        logger.LogTrace("ConvertForumCategories called");

        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var oldCategories = await oldcon.QueryAsync<oldsbs.ForumCategories>("select * from forumcategories");
            logger.LogInformation($"Found {oldCategories.Count()} forumcategories in old database");

            //Each category is another system content with create perms for a general audience. This is so people can
            //create threads inside. But, in the future, we can remove the create perm from specific categories!
            foreach(var oldCategory in oldCategories)
            {
                var newCategory = await AddSystemContent(new Db.Content {
                    literalType = "forumcategory",
                    name = oldCategory.name,
                    description = oldCategory.description,
                }, con, trans, true);
                //Now link the old fcid just in case
                await con.InsertAsync(CreateValue(newCategory.id, "fcid", oldCategory.fcid));
                await con.InsertAsync(CreateValue(newCategory.id, "permissions", oldCategory.permissions)); //NOTE: THEY'RE ALL 0, THERE'S NO REASON TO DO THIS
            }

            logger.LogInformation($"Inserted {oldCategories.Count()} forum categories owned by super {config.SuperUserId}");
        });
    }

    protected async Task ConvertForumThreads()
    {
        logger.LogTrace("ConvertForumThreads called");

        //We have to get the fcid to id mapping
        var categoryMapping = await GetOldToNewMapping("fcid", "forumcategory");

        await PerformChunkedTransfer<oldsbs.ForumThreads>("forumthreads", "ftid", async (oldcon, con, trans, oldThreads, start) =>
        {
            //Each category is another system content with create perms for a general audience. This is so people can
            //create threads inside. But, in the future, we can remove the create perm from specific categories!
            foreach(var oldThread in oldThreads)
            {
                var content = new Db.Content
                {
                    literalType = "forumthread",
                    parentId = categoryMapping.GetValueOrDefault(oldThread.fcid, 0),
                    createDate = oldThread.created, 
                    createUserId = oldThread.uid,
                    name = oldThread.title
                };

                if(content.parentId == 0)
                    throw new InvalidOperationException($"Couldn't find matching forum category fcid={oldThread.fcid} for thread '{oldThread.title}'({oldThread.ftid})");

                //Bit 4 is 'locked', which means it's readonly. Unfortunately there's no point getting rid of the user's self
                //permissions, since the API always gives you full permissions over your own content. That MUST be enforced
                //by the SSR frontend
                await AddGeneralPage(content, con, trans, (oldThread.status & 4) > 0);

                //Now link the old data just in case
                await con.InsertAsync(CreateValue(content.id, "ftid", oldThread.ftid), trans);
                await con.InsertAsync(CreateValue(content.id, "fcid", oldThread.fcid), trans);
                await con.InsertAsync(CreateValue(content.id, "views", oldThread.views), trans);
                await con.InsertAsync(CreateValue(content.id, "status", oldThread.status), trans);

                //Bit 1 from status is "important", which I think is an announcement thread. I don't know if we'll need that
                //anymore, but might as well indicate things easily
                if((oldThread.status & 1) > 0)
                {
                    await con.InsertAsync(CreateValue(content.id, $"important", true), trans);
                    logger.LogDebug($"Thread marked important: {CSTR(content)}");
                }

                //Bit 2 is a sticky thread
                if((oldThread.status & 2) > 0) 
                {
                    await con.InsertAsync(CreateValue(content.parentId, $"sticky:{content.id}", true), trans);
                    logger.LogDebug($"Stickied thread {CSTR(content)} to category {content.parentId}/fcid-{oldThread.fcid}");
                }
            }

            logger.LogInformation($"Inserted {oldThreads.Count} forum threads (chunk {start})");
        });
    }

    protected async Task ConvertForumPosts()
    {
        logger.LogTrace("ConvertForumPosts called");

        //We have to get the fcid to id mapping
        var threadMapping = await GetOldToNewMapping("ftid", "forumthread");

        await PerformChunkedTransfer<oldsbs.ForumPosts>("forumposts", "fpid", async (oldcon, con, trans, oldPosts, start) =>
        {
            foreach(var oldPost in oldPosts)
            {
                var message = new Db.Message
                {
                    contentId = threadMapping.GetValueOrDefault(oldPost.ftid, 0),
                    createDate = oldPost.created, 
                    createUserId = oldPost.uid,
                    text = oldPost.content
                };

                if(oldPost.edited.Ticks > 0 && oldPost.edited != oldPost.created)
                {
                    message.editDate = oldPost.edited;
                    message.editUserId = oldPost.euid;
                    logger.LogDebug($"Post {oldPost.fpid} was edited");
                }

                if(message.contentId == 0)
                    throw new InvalidOperationException($"Couldn't find matching forum thread ftid={oldPost.ftid} for post {oldPost.fpid}");

                var id = await con.InsertAsync(message, trans);

                //Now link the old data just in case. MAKE SURE THEY'RE MESSAGE VALUES!
                await con.InsertAsync(CreateMValue(message.id, "markup", "bbcode"), trans);
                await con.InsertAsync(CreateMValue(message.id, "fpid", oldPost.fpid), trans);
                await con.InsertAsync(CreateMValue(message.id, "ftid", oldPost.ftid), trans);
                await con.InsertAsync(CreateMValue(message.id, "status", oldPost.status), trans);
            }

            logger.LogInformation($"Inserted {oldPosts.Count} forum posts (chunk {start})");
        });
    }
}
        
