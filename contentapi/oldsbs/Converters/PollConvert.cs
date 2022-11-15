
using System.Data;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected async Task ConvertPolls()
    {
        logger.LogTrace("ConvertPolls called");

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var polls = await oldcon.QueryAsync<oldsbs.Polls>("select * from polls");

            foreach(var poll in polls)
            {
                var content = new Db.Content
                {
                    literalType = "poll",
                    createUserId = poll.uid,
                    createDate = poll.created,
                    name = poll.title,
                    text = poll.link
                };

                await AddGeneralPage(content, con, trans);
                await con.InsertAsync(CreateValue(content.id, "link", poll.link), trans);
                await con.InsertAsync(CreateValue(content.id, "pid", poll.pid), trans);
                await con.InsertAsync(CreateValue(content.id, "closed", poll.closed), trans);
                await con.InsertAsync(CreateValue(content.id, "hiddenresults", poll.hiddenresults), trans);
                await con.InsertAsync(CreateValue(content.id, "multivote", poll.multivote), trans);

                //Go get the options. there's too many to do at once, so might as well do it per thing.
                //Also, the votes are linked to the OPTIONS, so we'll need to do that at the same time
                var options = (await oldcon.QueryAsync<oldsbs.PollOptions>("select * from polloptions where pid = @pid", new { pid = poll.pid})).ToList();

                foreach(var option in options)
                {
                    var optionKey = "option-" + option.poid;

                    //Each option is just a value. use the ORIGINAL numbers, they probably have the order and they're valid!
                    await con.InsertAsync(CreateValue(content.id, optionKey, option.content), trans);

                    //This might be slow but I don't care, it's easier
                    var votes = (await oldcon.QueryAsync<oldsbs.PollVotes>("select * from pollvotes where poid = @poid", new {poid=option.poid})).ToList();

                    foreach(var vote in votes)
                    {
                        var message = new Db.Message
                        {
                            createDate = vote.created,
                            createUserId = vote.uid,
                            contentId = content.id,
                            text = optionKey
                        };
                        await con.InsertAsync(message, trans);
                    }
                }
            }
        });

        logger.LogInformation($"Converted all polls!");
    }
}
        
