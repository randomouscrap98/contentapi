using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected Task ConvertStoredValues()
    {
        logger.LogTrace("ConvertStoredValues called");

        //Use a transaction to make batch inserts much faster (on sqlite at least)
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var osv = await oldcon.QueryAsync<oldsbs.StoredValues>("select * from storedvalues");
            logger.LogInformation($"Found {osv.Count()} stored values in old database");

            var container = GetSystemContent("storedvalues");
            var id = await con.InsertAsync(container, trans);
            logger.LogInformation($"Inserted container for values ({id})");

            //ALL stored values are just values in this special container
            var newValues = osv.Select(x => CreateValue(id, x.name, x.value));

            logger.LogInformation($"Translated (in-memory) all the stored values");

            await con.InsertAsync(newValues, trans);
            logger.LogInformation($"Wrote {newValues.Count()} stored values into content {id}!");
        });
    }
}