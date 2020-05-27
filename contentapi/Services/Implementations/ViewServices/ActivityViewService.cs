using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //public class ActivityListenConfig
    //{
    //    public int limit {get;set;}
    //    public long firstId {get;set;}
    //    public long lastId {get;set;}
    //    public List<long> parentIds {get;set;}
    //}

    //public class ActivityListener
    //{
    //    public long userId {get;set;}

    //    public override string ToString()
    //    {
    //        return $"u{userId}";
    //    }
    //}

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
                x.action = x.action.Substring(1);

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

        public class TempGroup
        {
            public long userId {get;set;}
            public long contentId {get;set;}
            //public string action {get;set;}
        }

        public async Task<List<ActivityAggregateView>> SearchAggregateAsync(ActivitySearch search, Requester requester)
        {
            //Repeat code, be careful
            await FixWatchLimits(watchSource, requester, search.ContentLimit);

            var ids = activity.SearchIds(search, q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));

            var groups = await activity.GroupAsync<EntityRelation,TempGroup>(ids, x => new TempGroup(){ userId = x.entityId1, contentId = -x.entityId2 });

            return groups.ToLookup(x => x.Key.contentId).Select(x => new ActivityAggregateView()
            {
                id = x.Key,
                count = x.Sum(y => y.Value.count),
                lastActivity = x.Max(y => y.Value.lastDate),
                firstActivity = x.Min(y => y.Value.firstDate),
                lastId = x.Max(y => y.Value.lastId),
                //userActions = x.Select(y => new { user = y.Key.userId, action = y.Key.action })
                userIds = x.Select(y => y.Key.userId).Distinct().ToList()
            }).ToList();
        }
    }
}