using System.Runtime.ExceptionServices;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.Controllers;

public class ContentController : BaseController
{
    public ContentController(BaseControllerServices services)
          : base(services) { }

    [HttpGet("raw/{hash}")]
    public async Task<ActionResult<bool>> GetRawAsync([FromRoute] string hash)
    {
        try
        {
            var requester = GetUserId() ?? 0;
            var result = await CachedSearcher.SearchSingleType<ContentView>(requester, new SearchRequest()
            {
                type = nameof(RequestType.content),
                query = "hash = @hash",
                fields = "id, text, hash, literalType, lastRevisionId"
            }, new Dictionary<string, object> {
                { "hash", hash }
            });

            if (result.Count == 0)
                throw new NotFoundException($"No content found with hash {hash}");

            var content = result.First();

            Response.Headers.Add("ETag", $"{hash}-{content.lastRevisionId}");
            Response.Headers.Add("Content-Type", content.literalType);

            return Content(content.text); //File(result.Item1, result.Item2);
        }
        catch (Exception ex)
        {
            return await MatchExceptions(() =>
            {
                ExceptionDispatchInfo.Capture(ex).Throw();
                return Task.FromResult(true);
            });
        }
    }
}
