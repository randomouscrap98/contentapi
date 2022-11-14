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
                    hash = await GetTitleHash(oldThread.title, con),
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

        logger.LogInformation("Converted all forum threads!");
    }

    protected async Task ConvertForumPosts()
    {
        logger.LogTrace("ConvertForumPosts called");

        //We have to get the fcid to id mapping
        var threadMapping = await GetOldToNewMapping("ftid", "forumthread");
        int totalFlags = 0;

        await PerformChunkedTransfer<oldsbs.ForumPosts>("forumposts", "fpid", async (oldcon, con, trans, oldPosts, start) =>
        {
            var editedPosts = new List<long>();

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
                    editedPosts.Add(oldPost.fpid);
                }

                if(message.contentId == 0)
                    throw new InvalidOperationException($"Couldn't find matching forum thread ftid={oldPost.ftid} for post {oldPost.fpid}");

                var id = await con.InsertAsync(message, trans);

                //Now link the old data just in case. MAKE SURE THEY'RE MESSAGE VALUES!
                await con.InsertAsync(CreateMValue(id, "markup", "bbcode"), trans);
                await con.InsertAsync(CreateMValue(id, "fpid", oldPost.fpid), trans);
                await con.InsertAsync(CreateMValue(id, "ftid", oldPost.ftid), trans);
                await con.InsertAsync(CreateMValue(id, "status", oldPost.status), trans);

                //Since we're here, let's get the flags
                var flags = (await oldcon.QueryAsync<oldsbs.ForumFlags>("select * from forumflags where fpid=@id", new {id=oldPost.fpid})).ToList();

                if(flags.Count > 0)
                {
                    await con.InsertAsync(flags.Select(x => new Db.MessageEngagement() {
                        messageId = id,
                        userId = x.uid,
                        type = "flag"
                    }), trans);
                    logger.LogDebug($"Inserted {flags.Count} flags for post {id}({oldPost.fpid})");
                    totalFlags += flags.Count;
                }
            }

            logger.LogDebug($"{editedPosts.Count} edited posts this chunk");//These posts were edited: {string.Join(" ", editedPosts)}");
            logger.LogInformation($"Inserted {oldPosts.Count} forum posts (chunk {start})");
        });

        logger.LogInformation($"Converted all forum posts! Flagged: {totalFlags}");
    }


    protected async Task ConvertForumHistory()
    {
        logger.LogTrace("ConvertForumHistory called");

        await ConvertHistoryGeneral<oldsbs.ForumThreadsHistory>("forumthreads_history", "revisionDate", (m, h) =>
        {
            m.createDate = h.revisiondate;
            m.createUserId = config.SuperUserId;
        });

        await ConvertHistoryGeneral<oldsbs.ForumPostsHistory>("forumposts_history", "revisionDate", (m, h) =>
        {
            m.createDate = h.revisiondate;
            m.createUserId = config.SuperUserId;
        });

        logger.LogInformation("Converted all forum history!");
    }
}
        
