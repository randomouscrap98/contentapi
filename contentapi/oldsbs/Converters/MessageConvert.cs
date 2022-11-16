using System.Data;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected async Task ConvertMessages()
    {
        logger.LogTrace("ConvertMessages called");

        //Need to get all the message recipient elements, because we MUST see all the unique sets of users.
        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var oldRecipients = await oldcon.QueryAsync<oldsbs.MessageRecipients>("select * from messagerecipients");
            var messages = await oldcon.QueryAsync<oldsbs.Messages>("select * from messages");
            var mappedMessages = messages.ToDictionary(x => x.mid, y => y);
            logger.LogInformation($"Found {oldRecipients.Count()} message recipients in old database");

            var messageparent = await AddSystemContent("directmessages", con, trans, true);

            //A manual grouping but whatever
            var messageRecipients = new Dictionary<long, List<long>>();

            foreach(var or in oldRecipients)
            {
                if(!messageRecipients.ContainsKey(or.mid))
                    messageRecipients.Add(or.mid, new List<long>());
                messageRecipients[or.mid].Add(or.recipient);
            }

            //So for each recipient set, add a new private room
            var recipientsToContent = new Dictionary<string, long>();

            foreach(var mr in messageRecipients.OrderBy(x => x.Key))
            {
                var message = mappedMessages[mr.Key];
                var realRecipients = new HashSet<long>(mr.Value);
                realRecipients.Add(message.sender); //the sender wasn't included in the recipients list before

                var key = string.Join(",", realRecipients.OrderBy(x => x));

                if(!recipientsToContent.ContainsKey(key))
                {
                    //create content and save the mapping
                    var content = new Db.Content
                    {
                        parentId = messageparent.id,
                        name = "legacy private room (" + key + ")",
                        contentType = data.InternalContentType.page,
                        literalType = "directmessage",
                        createDate = message.senddate, //This is PROBABLY ok
                        hash = GetNextHash(),
                        createUserId = message.sender
                    };

                    var id = await con.InsertAsync(content, trans);
                    recipientsToContent.Add(key, id);

                    //Now you also need at LEAST the permissions
                    foreach(var recipient in realRecipients)
                        await con.InsertAsync(CreateBasicPermission(id, recipient), trans);
                }

                //We know there's a content for us, so take the message and put it in there
                var newMessage = new Db.Message
                {
                    text = message.content,
                    createDate = message.senddate,
                    createUserId = message.sender,
                    contentId = recipientsToContent[key]
                };

                var mid = await con.InsertAsync(newMessage, trans);

                //Need to track some old values
                await con.InsertAsync(CreateMValue(mid, "mid", message.mid));
            }

            logger.LogInformation($"Converted messages as {recipientsToContent.Count} private rooms");
        });
    }
}