using Dapper;
using Dapper.Contrib.Extensions;

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

                var values = new List<Db.ContentValue>
                {
                    CreateValue(0, "link", poll.link), 
                    CreateValue(0, "pid", poll.pid), 
                    CreateValue(0, "closed", poll.closed), 
                    CreateValue(0, "hiddenresults", poll.hiddenresults), 
                    CreateValue(0, "multivote", poll.multivote), 
                };

                //Go get the options. there's too many to do at once, so might as well do it per thing.
                //Also, the votes are linked to the OPTIONS, so we'll need to do that at the same time
                var options = (await oldcon.QueryAsync<oldsbs.PollOptions>("select * from polloptions where pid = @pid", new { pid = poll.pid})).ToList();
                var messages = new List<Db.Message>();

                foreach(var option in options)
                {
                    var optionKey = "option-" + option.poid;

                    //Each option is just a value. use the ORIGINAL numbers, they probably have the order and they're valid!
                    values.Add(CreateValue(0, optionKey, option.content));

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
                        messages.Add(message);
                    }
                }

                await AddGeneralPage(content, con, trans, false, true, null, values);
                messages.ForEach(x => x.contentId = content.id);
                await con.InsertAsync(messages, trans);
            }
        });

        logger.LogInformation($"Converted all polls!");
    }
}
        
