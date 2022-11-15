using System.Data;
using contentapi.Main;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public partial class OldSbsConvertController
{
    protected async Task ConvertOsp()
    {
        logger.LogTrace("ConvertOsp called");

        //These are roughly the same thing, just with different data. yeah idk...
        var groups = new Dictionary<long, oldsbs.OspGroup>();
        var contests = new Dictionary<long, oldsbs.OspContest>();
        var submissions = new List<oldsbs.OspSubmission>();
        var ospImages = new Dictionary<long, Tuple<Db.Message, string, string>>();

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            groups = (await oldcon.QueryAsync<oldsbs.OspGroup>("select * from ospgroup")).ToDictionary(x => x.ogid, y => y);
            contests = (await oldcon.QueryAsync<oldsbs.OspContest>("select * from ospcontest")).ToDictionary(x => x.ogid, y => y);
            submissions = (await oldcon.QueryAsync<oldsbs.OspSubmission>("select * from ospsubmission")).ToList();

            var contestMapping = new Dictionary<long, long>();

            //Add the osp contest/group combo as singular content. the type will be "ospcontest". They'll probably be system types!
            foreach(var group in groups)
            {
                var content = new Db.Content
                {
                    literalType = "ospcontest",
                    createDate = group.Value.createdon,
                    //createUserId = contests[group.Key].uid,
                    name = group.Value.name,
                };

                await AddSystemContent(content, con, trans, true);
                await con.InsertAsync(CreateValue(content.id, "ogid", group.Key), trans);
                await con.InsertAsync(CreateValue(content.id, "endDate", contests[group.Key].endon), trans);
                await con.InsertAsync(CreateValue(content.id, "open", contests[group.Key].isopen), trans);
                await con.InsertAsync(CreateValue(content.id, "endUserId", contests[group.Key].uid), trans);
                await con.InsertAsync(CreateValue(content.id, "link", contests[group.Key].link), trans);
                contestMapping.Add(group.Key, content.id);
            }

            foreach(var submission in submissions)
            {
                var message = new Db.Message
                {
                    createDate = submission.createdon,
                    createUserId = submission.uid,
                    text = submission.description,
                    contentId = contestMapping[submission.ogid]
                };

                var id = await con.InsertAsync(message, trans);
                await con.InsertAsync(CreateMValue(id, "osid", submission.osid));
                await con.InsertAsync(CreateMValue(id, "ogid", submission.ogid));
                await con.InsertAsync(CreateMValue(id, "filename", submission.filename));
                await con.InsertAsync(CreateMValue(id, "initialkey", submission.initialkey));
                ospImages.Add(id, Tuple.Create(message, submission.codeimage, submission.runimage));
            }

            logger.LogInformation($"For OSP, found {groups.Count} groups, {contests.Count} contests, and {submissions.Count} submissions in old database");
        });

        var imageUploads = new List<Db.MessageValue>();
        var httpClient = new HttpClient();

        //Because of files, have to do this outside of db transfer func
        foreach(var image in ospImages)
        {
            if(!string.IsNullOrWhiteSpace(image.Value.Item2))
            {
                var iview = await UploadImage(image.Value.Item2, image.Value.Item1.contentId, image.Value.Item1.createUserId, httpClient);
                if(iview != null)
                    imageUploads.Add(CreateMValue(image.Key, "codeimage", iview.hash));
            }
            if(!string.IsNullOrWhiteSpace(image.Value.Item3))
            {
                var iview = await UploadImage(image.Value.Item3, image.Value.Item1.contentId, image.Value.Item1.createUserId, httpClient);
                if(iview != null)
                    imageUploads.Add(CreateMValue(image.Key, "runimage", iview.hash));
            }
        }

        logger.LogInformation($"Uploaded all osp images ({ospImages.Count} sets)");

        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            foreach(var ivalue in imageUploads)
                await con.InsertAsync(ivalue, trans);
        });

        logger.LogInformation($"Linked all osp images ({imageUploads.Count})");
    }
}
        
