using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Implementations
{
    public class ContentViewService : BasePermissionViewService<ContentView, ContentSearch>
    {
        //Pulling the REAL service gives us some optimizations, since we get access to caching!
        protected CategoryViewService categoryService;

        protected CommentViewSource commentSource;
        protected ContentViewSource contentSource;
        protected WatchViewSource watchSource;
        protected VoteViewSource voteSource;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        protected static int rid = 0;

        public ContentViewService(ViewServicePack services, ILogger<ContentViewService> logger, 
            CategoryViewService categoryService, ContentViewSource converter,
            CommentViewSource commentSource, WatchViewSource watchSource, VoteViewSource voteSource, BanViewSource banSource) 
            : base(services, logger, converter, banSource) 
        { 
            this.categoryService = categoryService;
            this.commentSource = commentSource;
            this.watchSource = watchSource;
            this.contentSource = converter;
            this.voteSource = voteSource;
        }

        public override string EntityType => Keys.ContentType;
        public override string ParentType => Keys.CategoryType; 
        public override bool AllowOrphanPosts => true;

        public async Task SetupAsync()
        {
            var categories = await categoryService.SearchAsync(new CategorySearch(), new Requester() { system = true });
            cachedSupers = categoryService.viewSource.GetAllSupers(categories);
        }
        
        public override bool CanUser(Requester requester, string action, EntityPackage package)
        {
            var result = base.CanUser(requester, action, package);

            if(cachedSupers == null)
            {
                logger.LogWarning("CanUser called without cached supers");
            }
            else
            {
                var parentId = package.HasRelation(Keys.ParentRelation) ? package.GetRelation(Keys.ParentRelation).entityId1 : -1;
                result = result || action != Keys.ReadAction && 
                    (cachedSupers.ContainsKey(parentId) && cachedSupers[parentId].Contains(requester.userId) ||
                     cachedSupers.ContainsKey(package.Entity.id) && cachedSupers[package.Entity.id].Contains(requester.userId));
            }

            return result;
        }

        public override async Task<List<ContentView>> PreparedSearchAsync(ContentSearch search, Requester requester)
        {
            List<ContentView> baseResult = null;
            List<long> baseIds = null;

            Interlocked.Increment(ref rid);

            var t = services.timer.StartTimer(""); 

            try
            {
                baseResult = await base.PreparedSearchAsync(search, requester);
                baseIds = baseResult.Select(x => x.id).ToList();
                t.Name = $"[{rid}] content PreparedSearchAsync {baseResult.Count} ({string.Join(",", baseIds)})";
            }
            finally
            {
                services.timer.EndTimer(t);
            }

            if(baseIds.Count > 0 && search.IncludeAbout)
            {
                //TODO: STOP THIS MADNESS!!
                commentSource.JoinPermissions = false;
                watchSource.JoinPermissions = false;
                voteSource.JoinPermissions = false;

                try
                {
                    t = services.timer.StartTimer($"[{rid}] comment pull"); 
                    var comments = await commentSource.GroupAsync<EntityRelation, long>(
                        await commentSource.GetBaseQuery(new CommentSearch() { ParentIds = baseIds }), g=>g.relation, commentSource.PermIdSelector);
                    services.timer.EndTimer(t);
                    //var comments = await commentSource.GroupAsync<EntityRelation, long>(
                    //    await commentSource.SearchIds(new CommentSearch() { ParentIds = baseIds }), commentSource.PermIdSelector);

                    t = services.timer.StartTimer($"[{rid}] watch pull (both)"); 

                    //This requires intimate knowledge of how watches work. it's increasing the complexity/dependency,
                    //but at least... I don't know, it's way more performant. Come up with some system perhaps after
                    //you see what you need in other instances.
                    var watches = await watchSource.GroupAsync<EntityRelation, long>(
                        await watchSource.SearchIds(new WatchSearch() { ContentIds = baseIds }), watchSource.PermIdSelector);

                    var watchSearch = new WatchSearch();
                    watchSearch.UserIds.Add(requester.userId);
                    watchSearch.ContentIds.AddRange(baseIds);
                    var userWatching = await watchSource.SimpleSearchAsync(watchSearch);

                    services.timer.EndTimer(t);

                    //THIS could be an intensive query! Make sure you check the CPU usage!
                    t = services.timer.StartTimer($"[{rid}] vote pull"); 
                    var voteSearch = new VoteSearch();
                    voteSearch.UserIds.Add(requester.userId);
                    voteSearch.ContentIds.AddRange(baseIds);
                    var userVotes = await voteSource.SimpleSearchAsync(voteSearch);
                    services.timer.EndTimer(t);

                    t = services.timer.StartTimer($"[{rid}] vote aggregate"); 
                    var votes = new Dictionary<string, Dictionary<long, SimpleAggregateData>>();
                    foreach (var voteWeight in Votes.VoteWeights)
                    {
                        votes.Add(voteWeight.Key, await voteSource.GroupAsync<EntityRelation, long>(
                            await voteSource.SearchIds(new VoteSearch() { ContentIds = baseIds, Vote = voteWeight.Key }), voteSource.PermIdSelector)); //x => x.entityId2));
                    }
                    services.timer.EndTimer(t);

                    baseResult.ForEach(x =>
                    {
                        if (watches.ContainsKey(x.id))
                            x.about.watches = watches[x.id];
                        if (comments.ContainsKey(x.id))
                            x.about.comments = comments[x.id];

                        x.about.watching = userWatching.Any(y => y.contentId == x.id);
                        x.about.myVote = userVotes.FirstOrDefault(y => y.contentId == x.id)?.vote;

                        foreach (var voteWeight in Votes.VoteWeights)
                        {
                            x.about.votes.Add(voteWeight.Key, new SimpleAggregateData());

                            if (votes[voteWeight.Key].ContainsKey(x.id))
                                x.about.votes[voteWeight.Key] = votes[voteWeight.Key][x.id];
                        }
                    });
                }
                finally
                {
                    commentSource.JoinPermissions = true;
                    watchSource.JoinPermissions = true;
                    voteSource.JoinPermissions = true;
                }
            }
            else if(!search.IncludeAbout)
            {
                //Eventually, about is going away anyway
                baseResult.ForEach(x =>
                {
                    x.about = null;
                });
            }

            return baseResult;
        }
    }
}