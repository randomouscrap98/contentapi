//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using contentapi.Services.Constants;
//using contentapi.Services.Extensions;
//using contentapi.Views;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using Randomous.EntitySystem;
//
//namespace contentapi.Services.Implementations
//{
//    public class GeneralRelationListenConfig
//    {
//        public int limit {get;set;}
//        public long firstId {get;set;}
//        public long lastId {get;set;}
//        //public List<long> parentIds {get;set;}
//    }
//
//    public class GeneralRelationListener
//    {
//        public long userId {get;set;}
//    }
//
//    public class GeneralRelationViewService : BaseViewServices<GeneralRelationView, BaseSearch>//, IViewReadService<GeneralRelationView, BaseSearch>
//    {
//        //protected ContentViewService contentService;
//        protected ActivityViewSource activitySource;
//        protected CommentViewSource commentSource;
//        protected SystemConfig config;
//
//        public GeneralRelationViewService(ViewServicePack services, ILogger<GeneralRelationViewService> logger,
//            ActivityViewSource activitySource, CommentViewSource commentSource, SystemConfig config) 
//            : base(services, logger) 
//        { 
//            this.activitySource = activitySource;
//            this.commentSource = commentSource;
//            this.config = config;
//        }
//
//        //public Task SetupAsync() { return contentService.SetupAsync(); }
//
//        public override Task<List<GeneralRelationView>> PreparedSearchAsync(BaseSearch search, Requester requester)
//        {
//            throw new NotImplementedException();
//        }
//
//        public async Task<List<GeneralRelationView>> ListenAsync(GeneralRelationListenConfig listenConfig, Requester requester, CancellationToken token)
//        {
//            if(listenConfig.limit <= 0 || listenConfig.limit > 1000)
//                listenConfig.limit = 1000;
//
//            var listenId = new GeneralRelationListener() { userId = requester.userId } ;
//
//            //var stringParents = listenConfig.parentIds.Select(x => x.ToString());
//
//            var results = await services.provider.ListenAsync<EntityRelation>(listenId, (q) =>
//            {
//                var result = q.Where(x => (x.type == activitySource.EntityType || x.type == commentSource.EntityType)
//                    && x.id > listenConfig.firstId && x.id < listenConfig.lastId);// && (listenConfig.parentIds.Count == 0 || x.en));
//                return result.OrderByDescending(x => x.id).Take(listenConfig.limit);
//            }, config.ListenTimeout, token);
//
//            var aFunc = activitySource.PermIdSelector.Compile();
//            var cFunc = commentSource.PermIdSelector.Compile();
//
//            return results.Select(x => 
//            {
//                var view = new GeneralRelationView() { id = x.id };
//                if(x.type == activitySource.EntityType)
//                {
//                    view.type = "activity";
//                    view.contentId = aFunc(x);
//                }
//                else if(x.type == commentSource.EntityType)
//                {
//                    view.type = "comment";
//                    view.contentId = cFunc(x);
//                }
//                else
//                {
//                    throw new InvalidOperationException($"Somehow got an unexpected entity type: {x.type}");
//                }
//                return view;
//            }).ToList();
//        }
//        //public override Task<List<GeneralRelationView>> PreparedSearchAsync(BaseSearch search, Requester requester)
//        //{
//        //    logger.LogTrace($"GeneralRelation SearchAsync called by {requester}");
//
//        //    var basicQuery = Q<EntityRelation>()
//        //    return converter.SimpleSearchAsync(search, q =>
//        //        services.permissions.PermissionWhere(
//        //            q.Where(x => requester.system || x.relation.entityId1 == requester.userId), requester, Keys.ReadAction));
//        //    //You can only get your own watches.
//        //}
//
//        //public async Task<WatchView> GetByContentId(long contentId, Requester requester)
//        //{
//        //    var search = new WatchSearch();
//        //    search.ContentIds.Add(contentId);
//
//        //    var view = (await SearchAsync(search, requester)).OnlySingle();
//
//        //    if(view == null)
//        //        throw new NotFoundException($"No content found with id {contentId}");
//        //    
//        //    return view;
//        //}
//    }
//}