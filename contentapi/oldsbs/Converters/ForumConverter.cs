
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

            //Each category is just some content with global permissions. We'll need to mark the categories as special things,
            //but... this should be fine. I don't think these kinds of items (which could be added by super users) should have the system: moniker,
            //but rather that displaying all "pages" should simply look for specific literalContentType values rather than omitting any they don't want.
            //The monikers are still fine considering 

        });
    }
}
        
