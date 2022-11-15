using System.Data;
using System.Text.RegularExpressions;
using contentapi.Controllers;
using contentapi.Main;
using contentapi.Search;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace contentapi.oldsbs;

public class OldSbsConvertControllerConfig
{
    public string OldSbsConnectionString {get;set;} = "";
    public string BasePath {get;set;} = "";
    public string AvatarPath {get;set;} = "";
    public string BadgePath {get;set;} = "";
    public string OldDefaultAvatarRegex {get;set;} = "";
    public long SuperUserId {get;set;}
    public long ContentIdSkip {get;set;}
    public long MessageIdSkip {get;set;}
    public int MaxChunk {get;set;} = 1000;
    public Dictionary<string, long> BasePageTypes {get;set;} = new Dictionary<string, long>();
}

public partial class OldSbsConvertController : BaseController
{
    protected OldSbsConvertControllerConfig config;
    protected ILogger logger => services.logger;
    protected IFileService fileService;
    protected IGenericSearch searcher;
    protected IDbWriter writer;
    private static long nextId = 0;

    public OldSbsConvertController(BaseControllerServices services, OldSbsConvertControllerConfig config,
        IFileService fileService) : base(services)
    {
        this.config = config;
        this.fileService = fileService;
        this.searcher = services.dbFactory.CreateSearch();
        this.writer = services.dbFactory.CreateWriter();
    }

    protected IDbConnection GetOldSbsConnection()
    {
        return new MySqlConnection(config.OldSbsConnectionString);
    }

    protected string GetNextHash()
    {
        var id = Interlocked.Increment(ref nextId);
        return $"sbsconvert-{id}";
    }

    protected Task<Db.Content> AddSystemContent(string type, IDbConnection con, IDbTransaction trans, bool globalCreate = false)
    {
        return AddSystemContent(new Db.Content {
            literalType = type,
        }, con, trans, globalCreate);
    }

    /// <summary>
    /// System content is anything which may house data but which has no owner, only global read permission, no parent, and some special
    /// values to filter them out. They HAVE to have global read in order for them to be found AT ALL in the api
    /// </summary>
    /// <param name="whatfor"></param>
    /// <returns></returns>
    protected async Task<Db.Content> AddSystemContent(Db.Content content, IDbConnection con, IDbTransaction trans, bool globalCreate = false)
    {
        //Preset certain fields regardless of what was given
        content.createUserId = config.SuperUserId; //NOTE: it was changed such that most system content needs to be owned by the super user just in case.
        content.createDate = DateTime.UtcNow;
        content.deleted = false;
        content.hash = GetNextHash();
        content.contentType = data.InternalContentType.system;

        var id = await con.InsertAsync(content, trans);
        content.id = id;

        //And now we have to add the permission and the value
        await con.InsertAsync(globalCreate ? CreateBasicPermission(id) : CreateReadonlyPermission(id), trans);
        await con.InsertAsync(CreateSelfPermission(id, content.createUserId), trans);

        //WARN: DON'T use values to indicate system, even if system files might need that! The system content might
        //require those fields, and it's harder to filter out! 

        logger.LogInformation($"Inserted system content {CSTR(content)}");

        return content;
    }

    /// <summary>
    /// MOST user generated content should suffice with this. You should set the fields you care about; we ONLY set the hash and contentType
    /// </summary>
    /// <param name="content"></param>
    /// <param name="con"></param>
    /// <param name="trans"></param>
    /// <returns></returns>
    protected async Task<Db.Content> AddGeneralPage(Db.Content content, IDbConnection con, IDbTransaction trans, bool isReadonly = false, bool setBbcode = true)
    {
        //Assume the other fields are as people want them
        if(string.IsNullOrEmpty(content.hash))
            content.hash = GetNextHash();

        if(content.contentType == data.InternalContentType.none)
            content.contentType = data.InternalContentType.page;

        var id = await con.InsertAsync(content, trans);
        content.id = id;

        await con.InsertAsync(isReadonly ? CreateReadonlyPermission(id) : CreateBasicPermission(id));
        await con.InsertAsync(CreateSelfPermission(id, content.createUserId), trans);

        if(setBbcode)
            await con.InsertAsync(CreateValue(id, "markup", "bbcode"));

        logger.LogInformation($"Inserted general page {CSTR(content)}");

        return content;
    }

    /// <summary>
    /// For any table where the data may never be used and so will only be preserved as json
    /// </summary>
    /// <param name="table"></param>
    /// <param name="sort"></param>
    /// <param name="modify"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected async Task ConvertHistoryGeneral<T>(string table, string sort, Action<Db.Message, T>? modify = null)
    {
        var historyParent = new Db.Content();

        //First, need to insert the two parents
        await PerformDbTransfer(async (oldcon, con, trans) =>
        {
            historyParent = await AddSystemContent("raw:" + table, con, trans);
        });

        await PerformChunkedTransfer<T>(table, sort, async (oldcon, con, trans, oldHistory, start) =>
        {
            foreach(var history in oldHistory)
            {
                var message = new Db.Message()
                {
                    contentId = historyParent.id,
                    text = JsonConvert.SerializeObject(history) //Just put the whole old data as json in here.
                };

                if(modify != null)
                    modify(message, history);

                var id = await con.InsertAsync(message, trans);
                await con.InsertAsync(CreateMValue(id, "system", true));
                await con.InsertAsync(CreateMValue(id, "json", true));
            }

            logger.LogInformation($"Inserted {oldHistory.Count} {table} (chunk {start})");
        });
    }

    protected Db.ContentValue CreateValue(long contentId, string key, object? value) => new Db.ContentValue {
        contentId = contentId,
        key = key,
        value = JsonConvert.SerializeObject(value)
    };

    protected Db.MessageValue CreateMValue(long messageId, string key, object? value) => new Db.MessageValue {
        messageId = messageId,
        key = key,
        value = JsonConvert.SerializeObject(value)
    };

    protected Db.ContentPermission CreateReadonlyPermission(long contentId, long userId = 0) => new Db.ContentPermission {
        contentId = contentId,
        userId = userId,
        read = true 
    };

    protected Db.ContentPermission CreateBasicPermission(long contentId, long userId = 0) => new Db.ContentPermission {
        contentId = contentId,
        userId = userId,
        create = true,
        read = true 
    };

    protected Db.ContentPermission CreateSelfPermission(long contentId, long userId) => new Db.ContentPermission {
        contentId = contentId,
        userId = userId,
        create = true,
        read = true,
        update = true,
        delete = true
    };

    /// <summary>
    /// Produce the old to new mapping based on the given contentId query (should be the set of content you want to map)
    /// </summary>
    /// <param name="key"></param>
    /// <param name="query"></param>
    /// <param name="con"></param>
    /// <returns></returns>
    protected async Task<Dictionary<long, long>> GetOldToNewMappingQuery(string key, string query, IDbConnection? con = null)
    {
        IDbConnection realCon;
        bool created = false;

        if(con == null)
        {
            realCon = services.dbFactory.CreateRaw();
            created = true;
        }
        else
        {
            realCon = con;
        }

        try
        {
            var values = await realCon.QueryAsync<Db.ContentValue>(
                @$"select * from content_values 
                where key=""{key}""
                    and contentId in ({query})");

            var result = values.ToDictionary(k => JsonConvert.DeserializeObject<long>(k.value), v => v.contentId);

            logger.LogInformation($"{key} to content mapping for '{query}': " +
                string.Join(" ", result.Select(x => $"({x.Key}={x.Value})")));

            return result;
        }
        finally
        {
            if(created)
                realCon.Dispose();
        }
    }

    /// <summary>
    /// Assuming you saved the old id as a value, and the thing you're looking at has a unique type, this will return
    /// a simple mapping from old id to new id
    /// </summary>
    /// <param name="key"></param>
    /// <param name="type"></param>
    /// <param name="con"></param>
    /// <returns></returns>
    protected Task<Dictionary<long, long>> GetOldToNewMapping(string key, string type, IDbConnection? con = null)
    {
        return GetOldToNewMappingQuery(key, $"select id from content where literalType=\"{type}\"", con);
    }

    /// <summary>
    /// Basically all transfer functions will follow this same pattern, so might as well just give it to them.
    /// </summary>
    /// <param name="transferFunc"></param>
    /// <returns></returns>
    protected async Task PerformDbTransfer(Func<IDbConnection, IDbConnection, IDbTransaction, Task> transferFunc)
    {
        logger.LogTrace("PerformDbTransfer called");

        using(var oldcon = GetOldSbsConnection())
        {
            oldcon.Open();
            logger.LogDebug("Opened oldsbs connection!");

            using(var con = services.dbFactory.CreateRaw())
            {
                con.Open();
                logger.LogDebug("Opened contentapi connection!");

                using(var trans = con.BeginTransaction())
                {
                    await transferFunc(oldcon, con, trans);
                    trans.Commit();
                }
            }
        }
    }

    /// <summary>
    /// Too many things to operate on all at once? use this function to auto-chunk it all
    /// </summary>
    /// <param name="database"></param>
    /// <param name="order"></param>
    /// <param name="transferFunc"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected Task PerformChunkedTransfer<T>(string database, string order, Func<IDbConnection, IDbConnection, IDbTransaction, List<T>, int, Task> transferFunc)
    {
        logger.LogTrace($"PerformChunkedTransfer called for database {database}");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var count = await oldcon.ExecuteScalarAsync<int>($"select count(*) from {database}");
            logger.LogInformation($"Found {count} items in {database}; working in chunks of {config.MaxChunk}");

            for(var start = 0; start < count; start+=config.MaxChunk)
            {
                var oldStuff = (await oldcon.QueryAsync<T>($"select * from {database} order by {order} LIMIT {config.MaxChunk} OFFSET {start}")).ToList();
                await transferFunc(oldcon, con, trans, oldStuff, start);
            }
        });
    }

    /// <summary>
    /// Generate a reasonable title hash from the given title (with no collisions)
    /// </summary>
    /// <param name="title"></param>
    /// <param name="con"></param>
    /// <returns></returns>
    protected async Task<string> GetTitleHash(string title, IDbConnection con)
    {
        //First, make the title normal
        var normalizedTitle = Regex.Replace(Regex.Replace(title.ToLower(), @"\s+", "-"), @"[^a-z0-9\-]+", "");//.Substring(0, Math.Min(30));
        if(normalizedTitle.Length > 62)
            normalizedTitle = normalizedTitle.Substring(0, 62);
        var checkTitle = normalizedTitle;
        int next = 1;

        //Go see if this title is already used
        while(await con.ExecuteScalarAsync<int>("select count(*) from content where hash = @hash", new {hash=checkTitle}) > 0)
        {
            checkTitle = $"{normalizedTitle}-{++next}";

            if(next >= 10)
                throw new InvalidOperationException($"Couldn't find appropriate hash for title {title}");
        }

        return checkTitle;
    }

    protected async Task<data.Views.ContentView?> UploadImage(string link, long parentId, long userId, HttpClient httpClient)
    {
        Stream? fstream = null;

        try
        {
            if (link.StartsWith("http"))
            {
                logger.LogWarning($"Image {link} ({parentId}) is an external link, downloading it now");
                var response = await httpClient.GetAsync(link);
                response.EnsureSuccessStatusCode();
                fstream = await response.Content.ReadAsStreamAsync();
            }
            else
            {
                //The image link comes with the forward slash
                fstream = System.IO.File.Open(config.BasePath + link, FileMode.Open, FileAccess.Read);
            }

            //oops, we have to actually upload the file
            var fcontent = await fileService.UploadFile(new data.Views.ContentView
            {
                name = link,
                parentId = parentId,
                contentType = data.InternalContentType.file
            }, new UploadFileConfig(), fstream!, userId);

            logger.LogDebug($"Uploaded image for page {parentId}: {fcontent.name} ({fcontent.hash})");
            return fcontent;
            //imageList.Add(fcontent.hash);
        }
        catch (Exception ex)
        {
            logger.LogError($"Couldn't retrieve image {link} ({parentId}), skipping entirely: {ex}");
            return null;
            //continue;
        }
        finally
        {
            if (fstream != null)
                await fstream.DisposeAsync();
        }
    }

    protected Task SkipIds()
    {
        logger.LogTrace("SkipIds called");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            await con.InsertAsync(new Db.Content_Convert { id = config.ContentIdSkip, hash = "delete-later" }, trans);
            await con.InsertAsync(new Db.Message_Convert { id = config.MessageIdSkip }, trans);
            logger.LogInformation($"Inserted skip markers for content at {config.ContentIdSkip} and messages at {config.MessageIdSkip}");
        });
    }

    protected Task RemoveSkipMarkers()
    {
        logger.LogTrace("RemoveSkipMarkerscalled");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            await con.DeleteAsync(new Db.Content_Convert { id = config.ContentIdSkip }, trans);
            await con.DeleteAsync(new Db.Message_Convert { id = config.MessageIdSkip}, trans);
            logger.LogInformation($"Removed skip markers for content at {config.ContentIdSkip} and messages at {config.MessageIdSkip}");
        });
    }

    protected Task SanityChecks()
    {
        logger.LogTrace("SanityChecks called");
        return PerformDbTransfer(async (oldcon, con, trans) =>
        {
            var badContentCount = await con.ExecuteScalarAsync<int>("select count(*) from content where id < @id", new { id = config.ContentIdSkip });
            var badMessageCount = await con.ExecuteScalarAsync<int>("select count(*) from messages where id < @id", new { id = config.MessageIdSkip });

            logger.LogInformation($"Sanity checks passed!");
        });
    }


    protected string CSTR(Db.Content content) => $"'{content.name}'/{content.hash}({content.literalType})[{content.id}]";
    protected string CSTR(data.Views.ContentView content) => $"'{content.name}'/{content.hash}({content.literalType})[{content.id}]";

    [HttpGet()]
    public async Task ConvertAll()
    {
        logger.LogTrace("ConvertAll called! It expects an empty database and upload folder!");

        await SkipIds();

        //NOTE: all these conversion functions are put into separate files because they're so big
        await ConvertUsers();
        await ConvertUserSettings();
        await ConvertBans();
        await UploadAvatars(); //Because of our skip system, the ids for avatars no longer matter. To make things look nice, they should still come after content though
        await ConvertStoredValues();
        await ConvertBadgeGroups();
        await ConvertBadges();
        await ConvertBadgeAssignments();
        await ConvertBadgeHistory();
        await ConvertForumCategories();
        await ConvertForumThreads();
        await ConvertForumPosts();
        await ConvertForumHistory();
        await ConvertOsp();
        await ConvertPageCategories();
        await ConvertPages();
        await ConvertPageHistory();
        await ConvertInspector();
        await ConvertMessages();

        // Need to keep the skip markers around long enough to insert content with new ids
        await RemoveSkipMarkers();

        await SanityChecks();

        logger.LogInformation("Convert all complete?");
    }

    //There's just seemingly no other good place to put this!
    protected async Task ConvertInspector()
    {
        logger.LogTrace("ConvertInspector called");

        await ConvertHistoryGeneral<oldsbs.Inspector>("inspector", "lastuse", (m, h) =>
        {
            m.createDate = h.lastuse;
            // do we want it a super or the user we're inspecting? i think the user is fine...
            m.createUserId = h.uid;
        });

        logger.LogInformation("Converted all inspector data!");
    }
}