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

}
        
