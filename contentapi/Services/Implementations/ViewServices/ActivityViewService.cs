using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class ActivityViewService : BaseViewServices<ActivityView, ActivitySearch>, IViewReadService<ActivityView, ActivitySearch>
    {
        protected ActivityViewSource activity;
        protected CommentViewSource comments;
        protected WatchViewSource watchSource;

        public ActivityViewService(ViewServicePack services, ILogger<ActivityViewService> logger, 
            ActivityViewSource activity, CommentViewSource comments, WatchViewSource watchSource) 
            : base(services, logger) 
        { 
            this.activity = activity;
            this.comments = comments;
            this.watchSource = watchSource;
        }

        public override async Task<List<ActivityView>> PreparedSearchAsync(ActivitySearch search, Requester requester)
        {
            await FixWatchLimits(watchSource, requester, search.ContentLimit);

            var result = await activity.SimpleSearchAsync(search, (q) =>
                services.permissions.PermissionWhere(
                    q.Where(x => x.relation.type != $"{Keys.ActivityKey}{Keys.FileType}"),  //This may change sometime
                    requester, Keys.ReadAction, new PermissionExtras() { allowNegativeOwnerRelation = search.IncludeAnonymous })
            );

            result.ForEach(x => 
            {
                if(x.type == Keys.ContentType)
                    x.type = "content";
                else if(x.type == Keys.CategoryType)
                    x.type = "category";
                else if(x.type == Keys.UserType)
                    x.type = "user";
                else if(x.type == Keys.FileType)
                    x.type = "file";
            });

            return result;
        }
    }
}