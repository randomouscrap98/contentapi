using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class CombinedActivitySearch : ActivitySearch
    {
        public bool IncludeAnonymous {get;set;}
        //public TimeSpan RecentCommentTime {get;set;}
    }

    public class ActivityViewService : BaseViewServices<ActivityView, CombinedActivitySearch>, IViewReadService<ActivityView, CombinedActivitySearch>
    {
        protected ActivityViewSource activity;
        protected CommentViewSource comments;

        public ActivityViewService(ViewServicePack services, ILogger<ActivityViewService> logger, 
            ActivityViewSource activity, CommentViewSource comments) 
            : base(services, logger) 
        { 
            this.activity = activity;
            this.comments = comments;
        }

        public override async Task<List<ActivityView>> PreparedSearchAsync(CombinedActivitySearch search, Requester requester)
        {
            var result = await activity.SimpleSearchAsync(search, (q) =>
                services.permissions.PermissionWhere(
                    q.Where(x => x.relation.type != $"{Keys.ActivityKey}{Keys.FileType}"),  //This may change sometime
                    requester, Keys.ReadAction, new PermissionExtras() { allowNegativeOwnerRelation = search.IncludeAnonymous })
            );
            
            result.ForEach(x => x.contentType = x.contentType.Substring(Keys.ContentType.Length));

            return result;
        }

        //public async Task<List<CommentActivityView>> SearchCommentsAsync(CombinedActivitySearch search, Requester requester)
        //{
        //    var result = new List<CommentActivityView>();

        //    //No matter the search, get comments for up to the recent thing.
        //    if(search.RecentCommentTime.Ticks > 0)
        //    {
        //        var commentSearch = new CommentSearch()
        //        {
        //            CreateStart = DateTime.Now.Subtract(search.RecentCommentTime),
        //            Reverse = true
        //        };

        //        //This is a little heavy... we pull all the comment content as well. If there's been like...
        //        //10,000 comments each with 100 characters in the past day, that's 1 megabyte at least. Ah well...
        //        //wait until it becomes a problem to fix it. Overengineering can be just as bad as no
        //        var finalComments = await comments.SimpleSearchAsync(commentSearch, (q) =>
        //            services.permissions.PermissionWhere(q, requester, Keys.ReadAction));

        //        foreach(var group in finalComments.ToLookup(x => x.parentId))
        //        {
        //            var commentActivity = new CommentActivityView()
        //            {
        //                count = group.Count(),
        //                parentId = group.Key,
        //                userIds = group.Select(x => x.createUserId).Distinct().ToList(),
        //                lastDate = group.Max(x => (DateTime)x.createDate),
        //            };

        //            //Apply current timezone to the datetime. This MAY be dangerous
        //            //commentActivity.lastDate = new DateTime(commentActivity.lastDate.Ticks, DateTime.Now.Kind);

        //            result.Add(commentActivity);
        //        }
        //    }

        //    return result;
        //}
    }
}