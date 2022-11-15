using System.Data;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertPageCategories()
    {
        logger.LogTrace("ConvertPageCategories called");

        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var oldCategories = await oldcon.QueryAsync<oldsbs.Categories>("select * from categories order by pcid,cid");
            logger.LogInformation($"Found {oldCategories.Count()} categories in old database");

            var oldNewMapping = new Dictionary<long, long>();
            var rootPageTypeCategories = config.BasePageTypes.ToDictionary(k => k.Key, v => new List<long>{v.Value});

            logger.LogInformation($"Root category start lists: " + JsonConvert.SerializeObject(rootPageTypeCategories));

            //Each category is another system content with create perms for a general audience. This is so people can
            //create threads inside. But, in the future, we can remove the create perm from specific categories!
            foreach(var oldCategory in oldCategories)
            {
                var newCategory = await AddSystemContent(new Db.Content {
                    literalType = "category",
                    name = oldCategory.name,
                    description = oldCategory.description,
                    parentId = oldNewMapping.GetValueOrDefault(oldCategory.pcid, 0)
                }, con, trans, true);

                oldNewMapping.Add(oldCategory.cid, newCategory.id);

                //Put the right "allowed content" value based on the root category. This ONLY WORKS if the categories are read in order!
                bool found = false;

                //This is a hunt for the very root categories
                foreach(var kv in rootPageTypeCategories)
                {
                    if(kv.Value.Contains(oldCategory.pcid))
                    {
                        found = true;
                        kv.Value.Add(oldCategory.cid); // log ourselves as part of the group
                        await con.InsertAsync(CreateValue(newCategory.id, "forcontent", kv.Key));
                        break;
                    }
                }

                if(found == false) //This is fine as a warning now; they just won't BE for "any" category
                    logger.LogWarning($"Can't find root parent for category {CSTR(newCategory)}");

                //Now link the old values just in case
                await con.InsertAsync(CreateValue(newCategory.id, "cid", oldCategory.cid));
                await con.InsertAsync(CreateValue(newCategory.id, "pcid", oldCategory.pcid));
                await con.InsertAsync(CreateValue(newCategory.id, "alwaysavailable", oldCategory.alwaysavailable)); //NOTE: THEY'RE ALL 0, THERE'S NO REASON TO DO THIS
                await con.InsertAsync(CreateValue(newCategory.id, "permissions", oldCategory.permissions)); //NOTE: THEY'RE ALL 0, THERE'S NO REASON TO DO THIS
            }

            logger.LogInformation($"Inserted {oldCategories.Count()} categories owned by super {config.SuperUserId}");
        });
    }

    //This function does a lot of work because it's a bit more difficult to link to page data, so it's easier to just do it all
    //for each page as its pulled
    protected async Task ConvertPages()
    {
        logger.LogTrace("ConvertPages called");

        var inverseBasePageTypes = config.BasePageTypes.ToDictionary(k => k.Value, v => v.Key);
        var categoryMapping = await GetOldToNewMapping("cid", "category");
        var allImages = new Dictionary<long, List<oldsbs.PageImages>>();

        await PerformChunkedTransfer<oldsbs.Pages>("pages", "pid", async (oldcon, con, trans, oldPages, start) =>
        {
            foreach(var oldPage in oldPages)
            {
                //Need to pull all the related content
                var parameters = new { id = oldPage.pid };
                var categories = (await oldcon.QueryAsync<oldsbs.PageCategories>("select * from pagecategories where pid=@id", parameters)).ToList();
                var keywords = (await oldcon.QueryAsync<oldsbs.PageKeywords>("select * from pagekeywords where pid=@id", parameters)).ToList();
                var images = (await oldcon.QueryAsync<oldsbs.PageImages>("select * from pageimages where pid=@id", parameters)).ToList();
                var votes = (await oldcon.QueryAsync<oldsbs.PageVotes>("select * from pagevotes where pid=@id", parameters)).ToList();
                var comments = (await oldcon.QueryAsync<oldsbs.Comments>("select * from comments where pid=@id and created!='0000-00-00 00:00:00' order by pcid", parameters)).ToList();
                //Remember: don't need authors because there are none
                //Also: not actually doing anything with images, just... there for counting sake

                var type = "";
                var pagePrint = $"'{oldPage.title}({oldPage.pid})";

                foreach(var c in categories)
                    if(inverseBasePageTypes.ContainsKey(c.cid))
                        type = inverseBasePageTypes[c.cid];
                
                if(string.IsNullOrEmpty(type))
                    throw new InvalidOperationException($"Couldn't find page type for page {pagePrint}");

                //Also, parse the body. IDK if this will always work tbh...
                var bodyJson = JsonConvert.DeserializeObject<PageBody>(oldPage.body) ?? throw new InvalidOperationException($"Couldn't parse page body for page {pagePrint}");

                var page = new Db.Content
                {
                    parentId = 0,
                    createUserId = oldPage.euid,
                    createDate = oldPage.created,
                    description = bodyJson.tagline,
                    name = oldPage.title,
                    text = bodyJson.description,
                    contentType = data.InternalContentType.page,
                    hash = await GetTitleHash(oldPage.title, con),
                    literalType = type
                };

                var values = new List<Db.ContentValue>
                {
                    CreateValue(0, "pid", oldPage.pid), 
                    CreateValue(0, "edited", oldPage.edited), 
                    CreateValue(0, "dlkey", oldPage.dlkey), 
                    CreateValue(0, "version", oldPage.version), 
                    CreateValue(0, "size", oldPage.size), 
                    CreateValue(0, "dmca", oldPage.dmca), 
                    //NOTE: this needs to be translated into some other identifier, for searching of ptc/sb3/switch programs!
                    CreateValue(0, "support", oldPage.support), 
                    CreateValue(0, "translatedfor", bodyJson.translatedfor), 
                    CreateValue(0, "notes", bodyJson.notes), 
                    CreateValue(0, "instructions", bodyJson.instructions), 
                };

                values.AddRange(categories.Select(x => CreateValue(0, "tag:" + categoryMapping[x.cid], true)));

                var ckeywords = keywords.Select(x => new Db.ContentKeyword() {value = x.keyword}).ToList();

                await AddGeneralPage(page, con, trans, false, true, null, values, ckeywords);
                await con.InsertAsync(votes.Select(x => new Db.ContentEngagement() {contentId = page.id, type="vote", userId = x.uid, engagement=(x.vote == 0) ? "-" : "+"}), trans);

                //Now we have to convert all the comments. 
                await ConvertComments(comments, page.id, con, trans);

                //We can't do stuff with images just now, so we save them for later
                allImages.Add(page.id, images);

                logger.LogDebug($"Inserted page {CSTR(page)} with {votes.Count} votes, {keywords.Count} keywords, {categories.Count} categories, and {images.Count} images");
            }
            logger.LogInformation($"Inserted {oldPages.Count} submissions (chunk {start})");
        });

        logger.LogInformation($"Converted all pages! Now uploading/linking images");
        await ConvertPageImages(allImages);
    }

    /// <summary>
    /// Convert the given comments to messages on the given page, including linking parent comments. The order must be
    /// pre-set so parent messages are written first... this might be problematic
    /// </summary>
    /// <param name="comments"></param>
    /// <param name="contentId"></param>
    /// <returns></returns>
    private async Task ConvertComments(List<oldsbs.Comments> comments, long contentId, IDbConnection con, IDbTransaction trans)
    {
        //Need to keep track of old to new commentids
        var commentMapping = new Dictionary<long, long>();
        int editCount = 0;

        foreach(var comment in comments)
        {
            var message = new Db.Message
            {
                createDate = comment.created, 
                createUserId = comment.uid,
                editUserId = comment.euid,
                contentId = contentId,
                text = comment.content
            };

            if(comment.euid != null)//comment.edited != null && comment.edited?.Ticks > 0 && comment.edited != comment.created)
            {
                //Checking just euid might be enough!
                message.editDate = comment.edited;
                editCount++;
            }
            
            var id = await con.InsertAsync(message, trans);
            commentMapping.Add(comment.cid, id);

            if(comment.pcid != null)
            {
                await con.InsertAsync(CreateMValue(id, "pcid", comment.pcid));
                await con.InsertAsync(CreateMValue(id, "parent", commentMapping[comment.pcid.Value]));
            }

            await con.InsertAsync(CreateMValue(id, "status", comment.status));
            await con.InsertAsync(CreateMValue(id, "markup", "bbcode"));
            await con.InsertAsync(CreateMValue(id, "cid", comment.cid));
            await con.InsertAsync(CreateMValue(id, "pid", comment.pid));
        }

        logger.LogDebug($"Inserted {comments.Count} comments for page {contentId} ({editCount} edited)");
    }

    /// <summary>
    /// Convert all images collected from all pages, including upload and linking to content. This must be done outside
    /// the normal flow of conversion so we don't deadlock from transactions
    /// </summary>
    /// <param name="allImages"></param>
    /// <returns></returns>
    private async Task ConvertPageImages(Dictionary<long, List<oldsbs.PageImages>> allImages)
    {
        var writePageImagelist = new Dictionary<long, List<string>>();

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        //Now that we're out of that loop, we can upload the images
        foreach(var imageSet in allImages)
        {
            var imageList = new List<string>();
            foreach(var image in imageSet.Value.OrderBy(x => x.number)) 
            {
                var iview = await UploadImage(image.link, imageSet.Key, image.uid, httpClient);

                if(iview != null)
                    imageList.Add(iview.hash);
            }
            writePageImagelist.Add(imageSet.Key, imageList);
        }

        logger.LogInformation($"Uploaded all page images ({allImages.Sum(x => x.Value.Count)})! Now adding values to link pages to imagelist");

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            foreach(var imageList in writePageImagelist)
            {
                await con.InsertAsync(CreateValue(imageList.Key, "images", imageList.Value), trans);
            }
        });

        logger.LogInformation($"Added all imagelists to content ({writePageImagelist.Sum(x => x.Value.Count)})! Page conversion complete!");
    }

    protected async Task ConvertPageHistory()
    {
        logger.LogTrace("ConvertPageHistory called");

        await ConvertHistoryGeneral<oldsbs.PageCategoriesHistory>("pagecategories_history", "revision", (m, h)=> { m.createUserId = config.SuperUserId; }); 
        await ConvertHistoryGeneral<oldsbs.PageImagesHistory>("pageimages_history", "revision", (m, h)=> { m.createUserId = config.SuperUserId; }); 
        await ConvertHistoryGeneral<oldsbs.PageKeywordsHistory>("pagekeywords_history", "revision", (m, h)=> { m.createUserId = config.SuperUserId; }); 

        await ConvertHistoryGeneral<oldsbs.PagesHistory>("pages_history", "revisionDate", (m, h) =>
        {
            m.createDate = h.revisiondate;
            m.createUserId = config.SuperUserId;
        });

        await ConvertHistoryGeneral<oldsbs.CommentsHistory>("comments_history", "revisionDate", (m, h) =>
        {
            m.createDate = h.revisiondate;
            m.createUserId = config.SuperUserId;
        });

        logger.LogInformation("Converted all page history!");
    }
}
        
