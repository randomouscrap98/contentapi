using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.data;
using contentapi.data.Views;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class SpecializedSearchTests : ViewUnitTestBase //, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected IDbWriter writer;
    protected IGenericSearch searcher;

    public SpecializedSearchTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();

        searcher = fixture.GetGenericSearcher(); //GetService<IGenericSearch>();
        writer = fixture.GetWriter();

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Theory]
    [InlineData("No ids here!", "")]
    [InlineData("One uid here: %5%", "5")]
    [InlineData("%47% is a uid", "47")]
    [InlineData("And in %999% the middle", "999")]
    [InlineData("(%234%): hello", "234")]
    [InlineData("But what about %47%'s ugly duck? They ate %65%'s bread!", "47,65")]
    [InlineData("%1%%2%%3%%4%%10%-%9432%", "1,2,3,4,10,9432")]
    [InlineData("This... %f65% not an id. Also, %.65%, or %44.$, or %-10%, or %null%, just %% is nothi%#%ng", "")]
    public async Task SearchAsync_MessageUidsInText(string message, string uidString)
    {
        var uids = uidString.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => long.Parse(x)).ToList();

        var comment = GetNewCommentView(AllAccessContentId);
        comment.text = message;

        var writtenComment = await writer.WriteAsync(comment, NormalUserId);
        Assert.True(uids.OrderBy(x => x).SequenceEqual(writtenComment.uidsInText.OrderBy(x => x)));
    }

    [Fact]
    public async void SearchAsync_InMultiList()
    {
        //Write two comments with two users in them. See if chaining to that field works
        var comment1 = GetNewCommentView(AllAccessContentId);
        comment1.text = $"And it's %{NormalUserId}%";
        var comment2 = GetNewCommentView(AllAccessContentId);
        comment2.text = $"And it's also %{SuperUserId}%";

        var writtenComment1 = await writer.WriteAsync(comment1, NormalUserId);
        var writtenComment2 = await writer.WriteAsync(comment2, SuperUserId);

        //Now search specifically for those two comments, but with users chained to the uidsInText field
        var search = new SearchRequests()
        {
            requests = new List<SearchRequest> {
                new SearchRequest() {
                    type = "message",
                    fields = "*",
                    query = "id in @ids"
                },
                new SearchRequest() {
                    type = "user",
                    fields = "*",
                    query = "id in @message.uidsInText"
                }
            },
            values = new Dictionary<string, object> {
                { "ids", new[] { writtenComment1.id, writtenComment2.id }}
            }
        };

        var results = await searcher.Search(search, NormalUserId);
        var comments = searcher.ToStronglyTyped<MessageView>(results.objects["message"]);
        var users = searcher.ToStronglyTyped<UserView>(results.objects["user"]);

        Assert.Equal(2, comments.Count);
        Assert.Equal(2, users.Count);
        Assert.Contains(writtenComment1.id, comments.Select(x => x.id ));
        Assert.Contains(writtenComment2.id, comments.Select(x => x.id ));
        Assert.Contains(NormalUserId, users.Select(x => x.id ));
        Assert.Contains(SuperUserId, users.Select(x => x.id ));
    }

    //This regression test needs a standard configured dbwriter, which didn't really fit anywhere else but here
    [Fact]
    public async Task Regression_CommentDeleteEventNoFail()
    {
        //write a simple comment
        var comment = GetNewCommentView(AllAccessContentId);
        var writtenComment = await writer.WriteAsync(comment, NormalUserId);

        Assert.NotEqual(0, writtenComment.id);
        Assert.NotEqual(0, writtenComment.contentId);

        //Now delete it
        var deletedComment = await writer.DeleteAsync<MessageView>(writtenComment.id, NormalUserId);

        //That's the test, we just failed entirely before. We already know it's being reported
        //as an event from previous tests, so we're just making sure integration doesn't break it
        Assert.True(deletedComment.deleted);
        Assert.NotEqual(0, deletedComment.contentId);
        Assert.False(deletedComment.edited);
    }

    [Fact]
    public async Task Regression_SearchValuesForPinned()
    {
        //Write a comment to a known content
        var comment = GetNewCommentView(AllAccessContentId);
        var writtenComment = await writer.WriteAsync(comment, NormalUserId);

        //Now update said content to have more values
        var content = await searcher.GetById<ContentView>(AllAccessContentId);
        content.values.Add("pinned", new List<long> { writtenComment.id });
        var writtenContent = await writer.WriteAsync(content, NormalUserId);

        //Then construct a search for content and comments such that the comments are in the values
        var search = new SearchRequests()
        {
            values = new Dictionary<string, object> { },
            requests = new List<SearchRequest>() {
                //This searches ALL content, many of which will NOT have the pinned
                new SearchRequest() {
                    type = nameof(RequestType.content),
                    fields = "*"
                },
                new SearchRequest() {
                    type = nameof(RequestType.message),
                    fields = "*",
                    query = "id in @content.values.pinned"
                }
            }
        };

        var searchResult = await searcher.SearchUnrestricted(search);
        var searchMessages = searcher.ToStronglyTyped<MessageView>(searchResult.objects[nameof(RequestType.message)]);
        var searchContent = searcher.ToStronglyTyped<MessageView>(searchResult.objects[nameof(RequestType.content)]);

        Assert.Contains(writtenComment.id, searchMessages.Select(x => x.id));
        Assert.Contains(AllAccessContentId, searchContent.Select(x => x.id));
    }

    [Theory]
    [InlineData("!valuelike({{hello}},{{\"%KENOBI%\"}})", true)]
    [InlineData("!valuekeyin(@keys)", true)]
    [InlineData("!valuekeynotin(@keys)", false)]
    [InlineData("!valuekeynotlike({{hello}})", false)]
    public async Task Regression_CommentValueSearch(string query, bool shouldFind)
    {
        //write a simple comment
        var comment = GetNewCommentView(AllAccessContentId);
        comment.values.Add("hello", "GENERAL KENOBI");
        var writtenComment = await writer.WriteAsync(comment, NormalUserId);

        //Now go do a value search.
        var found = await searcher.SearchSingleType<MessageView>(NormalUserId, new SearchRequest()
        {
            type = nameof(RequestType.message),
            fields = "*",
            query = query
        }, new Dictionary<string, object> {
            { "keys", new[] {"hello"}}
        });

        if(shouldFind)
        {
            Assert.Single(found);
            Assert.Equal(writtenComment.id, found.First().id);
            Assert.Contains("hello", found.First().values.Keys);
            Assert.Equal("GENERAL KENOBI", found.First().values["hello"]);
        }
        else
        {
            Assert.True(found.Count > 1);
            Assert.DoesNotContain(writtenComment.id, found.Select(x => x.id));
        }
    }

    //Specialized keyword tests to make sure permissions and queries are working
    [Theory]
    [InlineData(0, AllAccessContentId, true)]
    [InlineData(0, SuperAccessContentId, false)]
    [InlineData(NormalUserId, AllAccessContentId, true)]
    [InlineData(NormalUserId, SuperAccessContentId, false)]
    [InlineData(SuperUserId, AllAccessContentId, true)]
    [InlineData(SuperUserId, SuperAccessContentId, true)]
    public async Task GenericSearch_Search_KeywordAggregate_Permissions(long userId, long contentId, bool allowed)
    {
        //First, update some content with some special keywords
        var content = await searcher.GetById<ContentView>(RequestType.content, contentId); //SearchSingleTypeUnrestricted<ContentView>()
        content.keywords = new List<string> { "mega", "hecking", "chonker" }; //note that we REPLACE the existing keywords
        var writtenContent = await writer.WriteAsync(content, SuperUserId);

        //OK, now that there are keywords, go try to get them (this ALSO tests the contentId get)
        var keywords = await searcher.SearchSingleType<KeywordAggregateView>(userId, new SearchRequest()
        {
            type = nameof(RequestType.keyword_aggregate),
            fields = "*",
            query = "contentId = {{" + contentId + "}}"
        });

        //No matter what, we can always assert that there are no OTHER keywords in it...
        Assert.All(keywords, x =>
        {
            Assert.Contains(x.value, writtenContent.keywords);
            Assert.Equal(1, x.count);
        });

        if(allowed)
        {
            Assert.True(keywords.Count > 0);
            Assert.Equal(writtenContent.keywords.Count, keywords.Count);
            Assert.Equal(new HashSet<string>(writtenContent.keywords), new HashSet<string>(keywords.Select(x => x.value)));
        }
        else
        {
            Assert.Empty(keywords);
        }
    }

    //Special test to ensure value searching works
    [Theory]
    [InlineData("", "", "mega, hecking, chonker")]
    [InlineData("value LIKE @thing", "m%", "mega")]
    [InlineData("value LIKE @thing", "%i%", "hecking")]
    [InlineData("value = @thing", "chonker", "chonker")]
    [InlineData("value LIKE @thing", "%c%", "hecking, chonker")]
    public async Task GenericSearch_Search_KeywordAggregate_ValueQuery(string query, string value, string results)
    {
        var expected = results.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

        //First, create some content with some special keywords
        var content = GetNewPageView();
        content.keywords = new List<string> { "mega", "hecking", "chonker" }; //note that we REPLACE the existing keywords
        var writtenContent = await writer.WriteAsync(content, SuperUserId);

        var queryParts = new List<string> { "contentId = {{" + writtenContent.id + "}}" };

        if(!string.IsNullOrEmpty(query))
            queryParts.Add(query);

        //OK, now that there are keywords, go try to get them (this ALSO tests the contentId get)
        var keywords = await searcher.SearchSingleType<KeywordAggregateView>(NormalUserId, new SearchRequest()
        {
            type = nameof(RequestType.keyword_aggregate),
            fields = "*",
            query = String.Join(" and ", queryParts)
        }, new Dictionary<string, object>() { {"thing", value} });

        //No matter what, we can always assert that there are no OTHER keywords in it...
        Assert.All(keywords, x =>
        {
            Assert.Contains(x.value, writtenContent.keywords);
            Assert.Equal(1, x.count);
        });

        //Now a simple test: are the results the same as the ones we expect?
        Assert.Equal(new HashSet<string>(expected), new HashSet<string>(keywords.Select(x => x.value)));
        Assert.Equal(expected.Count, keywords.Count);
    }

    [Fact]
    public async Task GenericSearch_Search_ContentType()
    {
        //create content of the various types
        var module = GetNewModuleView();
        var file = GetNewFileView();
        var page = GetNewPageView();

        //Set keywords for each
        module.keywords.Add("ismodule");
        file.keywords.Add("isfile");
        page.keywords.Add("ispage");

        //Write them all
        var writtenModule = await writer.WriteAsync(module, SuperUserId);
        var writtenFile = await writer.WriteAsync(file, SuperUserId);
        var writtenPage = await writer.WriteAsync(page, SuperUserId);

        var getKeywordsOfType = new Func<InternalContentType, Task<List<KeywordAggregateView>>>((t) =>
            searcher.SearchSingleType<KeywordAggregateView>(SuperUserId, new SearchRequest()
            {
                type = nameof(RequestType.keyword_aggregate),
                fields = "*",
                query = "!contenttype(@type)"
            }, new Dictionary<string, object>() { {"type", t} })
        );

        //Now do three different tests, one for each type, and ensure you only get the one keyword and not the other two
        var keywords = await getKeywordsOfType(InternalContentType.module);
        var ks = keywords.Select(x => x.value);
        Assert.Contains("ismodule", ks);
        Assert.DoesNotContain("isfile", ks);
        Assert.DoesNotContain("ispage", ks);

        keywords = await getKeywordsOfType(InternalContentType.file);
        ks = keywords.Select(x => x.value);
        Assert.Contains("isfile", ks);
        Assert.DoesNotContain("ismodule", ks);
        Assert.DoesNotContain("ispage", ks);

        keywords = await getKeywordsOfType(InternalContentType.page);
        ks = keywords.Select(x => x.value);
        Assert.Contains("ispage", ks);
        Assert.DoesNotContain("ismodule", ks);
        Assert.DoesNotContain("isfile", ks);
    }

    [Fact]
    public async Task GenericSearch_Search_CorrectMessageEngagement()
    {
        //Add a message to put engagement on
        var message = GetNewCommentView(AllAccessContentId);
        var writtenMessage = await writer.WriteAsync(message, SuperUserId);

        //Add a particular subset of engagement to the message
        var e1 = await writer.WriteAsync(GetMessageEngagementView(writtenMessage.id, "vote", "ok"), NormalUserId);
        var e2 = await writer.WriteAsync(GetMessageEngagementView(writtenMessage.id, "vote", "bad"), SuperUserId);
        var e3 = await writer.WriteAsync(GetMessageEngagementView(writtenMessage.id, "reaction", "💙"), NormalUserId);
        var e4 = await writer.WriteAsync(GetMessageEngagementView(writtenMessage.id, "reaction", "💙"), SuperUserId);

        var checkMessages = await searcher.SearchSingleType<MessageView>(NormalUserId, new SearchRequest()
        {
            type = nameof(RequestType.message),
            query = "id = @id",
            fields = "*"
        }, new Dictionary<string, object> { {"id",writtenMessage.id}});

        Assert.Single(checkMessages);
        var engagement = checkMessages.First().engagement;
        Assert.True(engagement.ContainsKey("vote"));
        Assert.True(engagement.ContainsKey("reaction"));
        Assert.True(engagement["vote"].ContainsKey("ok"));
        Assert.True(engagement["vote"].ContainsKey("bad"));
        Assert.Equal(1, engagement["vote"]["ok"]);
        Assert.Equal(1, engagement["vote"]["bad"]);
        Assert.True(engagement["reaction"].ContainsKey("💙"));
        Assert.Equal(2, engagement["reaction"]["💙"]);
    }
}