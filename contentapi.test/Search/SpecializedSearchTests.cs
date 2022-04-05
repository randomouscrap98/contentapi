using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.Views;
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

        searcher = fixture.GetService<IGenericSearch>();
        writer = fixture.GetService<IDbWriter>();

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
        var comments = searcher.ToStronglyTyped<MessageView>(results.data["message"]);
        var users = searcher.ToStronglyTyped<UserView>(results.data["user"]);

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
}