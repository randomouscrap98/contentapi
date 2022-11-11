
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
                var newCategory = await AddSystemContent(new Db.Content
                {
                    literalType = "forumcategory",
                    name = oldCategory.name,
                    description = oldCategory.description,
                }, con, trans, true);
                //Now link the old fcid just in case
                await con.InsertAsync(CreateValue(newCategory.id, "fcid", oldCategory.fcid));
            }

            logger.LogInformation($"Inserted {oldCategories.Count()} forum categories owned by super {config.SuperUserId}");
        });
    }
}
        
