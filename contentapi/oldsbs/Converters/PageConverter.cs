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

    protected async Task ConvertPages()
    {
        logger.LogTrace("ConvertPages called");

        var inverseBasePageTypes = config.BasePageTypes.ToDictionary(k => k.Value, v => v.Key);
        var categoryMapping = await GetOldToNewMapping("cid", "category");

        await PerformChunkedTransfer<oldsbs.Pages>("pages", "pid", async (oldcon, con, trans, oldPages, start) =>
        {
            foreach(var oldPage in oldPages)
            {
                //Need to pull all the related content
                var parameters = new { id = oldPage.pid };
                var categories = (await con.QueryAsync<oldsbs.PageCategories>("select * from pagecategories where pid=@id", parameters)).ToList();
                var keywords = (await con.QueryAsync<oldsbs.PageKeywords>("select * from pagekeywords where pid=@id", parameters)).ToList();
                var images = (await con.QueryAsync<oldsbs.PageImages>("select * from pageimages where pid=@id", parameters)).ToList();
                var votes = (await con.QueryAsync<oldsbs.PageVotes>("select * from pagevotes where pid=@id", parameters)).ToList();
                //Remember: don't need authors because there are none

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
                    literalType = type
                };

                await AddGeneralPage(page, con, trans);
                await con.InsertAsync(keywords.Select(x => new Db.ContentKeyword() {contentId = page.id, value = x.keyword}), trans);
                await con.InsertAsync(categories.Select(x => CreateValue(page.id, "tag:" + categoryMapping[x.cid], true)), trans);
                await con.InsertAsync(votes.Select(x => new Db.ContentEngagement() {contentId = page.id, type="vote", userId = x.uid, engagement=(x.vote == 0) ? "-" : "+"}), trans);
                await con.InsertAsync(CreateValue(page.id, "pid", oldPage.pid), trans);
                await con.InsertAsync(CreateValue(page.id, "edited", oldPage.edited), trans);
                await con.InsertAsync(CreateValue(page.id, "dlkey", oldPage.dlkey), trans);
                await con.InsertAsync(CreateValue(page.id, "version", oldPage.version), trans);
                await con.InsertAsync(CreateValue(page.id, "size", oldPage.size), trans);
                await con.InsertAsync(CreateValue(page.id, "dmca", oldPage.dmca), trans);
                //NOTE: this needs to be translated into some other identifier, for searching of ptc/sb3/switch programs!
                await con.InsertAsync(CreateValue(page.id, "support", oldPage.support), trans);
                await con.InsertAsync(CreateValue(page.id, "translatedfor", bodyJson.translatedfor), trans);
                await con.InsertAsync(CreateValue(page.id, "notes", bodyJson.notes), trans);
                await con.InsertAsync(CreateValue(page.id, "instructions", bodyJson.instructions), trans);
                logger.LogDebug($"Inserted page {CSTR(page)} with {votes.Count} votes, {keywords.Count} keywords, {categories.Count} categories, and {images.Count} images");
            }
            logger.LogInformation($"Inserted {oldPages.Count} submissions (chunk {start})");
        });
    }
}
        
