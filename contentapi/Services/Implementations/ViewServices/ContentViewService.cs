using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        protected CacheService<string, List<ContentView>> cache;
        
        protected Dictionary<long, List<long>> cachedSupers = null;

        protected static int rid = 0;

        public ContentViewService(ViewServicePack services, ILogger<ContentViewService> logger, 
            CategoryViewService categoryService, ContentViewSource converter,
            CacheService<string, List<ContentView>> cacheService,
            CommentViewSource commentSource, WatchViewSource watchSource, VoteViewSource voteSource, BanViewSource banSource) 
            : base(services, logger, converter, banSource) 
        { 
            this.categoryService = categoryService;
            this.commentSource = commentSource;
            this.watchSource = watchSource;
            this.contentSource = converter;
            this.voteSource = voteSource;
            this.cache = cacheService;
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

        //Track writes and deletes. ANY write/delete causes us to flush the full in-memory 
        public override Task<ContentView> WriteAsync(ContentView view, Requester requester)
        {
            cache.PurgeCache();
            return base.WriteAsync(view, requester);
        }

        public override Task<ContentView> DeleteAsync(long entityId, Requester requester)
        {
            cache.PurgeCache();
            return base.DeleteAsync(entityId, requester);
        }

        public override async Task<List<ContentView>> PreparedSearchAsync(ContentSearch search, Requester requester)
        {
            string key = JsonSerializer.Serialize(search) + JsonSerializer.Serialize(requester); 
            List<ContentView> baseResult = null;

            if(cache.GetValue(key, ref baseResult))
                return baseResult;

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

            if(baseResult.Count == 0)
                return baseResult;

            if(search.IncludeAbout != null && search.IncludeAbout.Count > 0)
            {
                var desired = search.IncludeAbout.Select(x => x.ToLower());

                //TODO: STOP THIS MADNESS!!
                commentSource.JoinPermissions = false;
                watchSource.JoinPermissions = false;
                voteSource.JoinPermissions = false;

                try
                {
                    Dictionary<long, SimpleAggregateData> comments = null;
                    Dictionary<long, SimpleAggregateData> watches = null;
                    Dictionary<string, Dictionary<long, SimpleAggregateData>> votes = null;
                    List<WatchView> userWatching = null;
                    List<VoteView> userVotes = null;

                    if(desired.Any(x => x.StartsWith("comment")))
                    {
                        t = services.timer.StartTimer($"[{rid}] comment pull");
                        comments = await commentSource.GroupAsync<EntityRelation, long>(
                            await commentSource.GetBaseQuery(new CommentSearch() { ParentIds = baseIds }), g => g.relation, commentSource.PermIdSelector);
                        services.timer.EndTimer(t);
                    }

                    if(desired.Any(x => x.StartsWith("watch")))
                    {
                        t = services.timer.StartTimer($"[{rid}] watch pull (both)");

                        //This requires intimate knowledge of how watches work. it's increasing the complexity/dependency,
                        //but at least... I don't know, it's way more performant. Come up with some system perhaps after
                        //you see what you need in other instances.
                        watches = await watchSource.GroupAsync<EntityRelation, long>(
                            await watchSource.SearchIds(new WatchSearch() { ContentIds = baseIds }), watchSource.PermIdSelector);

                        var watchSearch = new WatchSearch();
                        watchSearch.UserIds.Add(requester.userId);
                        watchSearch.ContentIds.AddRange(baseIds);
                        userWatching = await watchSource.SimpleSearchAsync(watchSearch);

                        services.timer.EndTimer(t);
                    }

                    if(desired.Any(x => x.StartsWith("watch")))
                    {
                        //THIS could be an intensive query! Make sure you check the CPU usage!
                        t = services.timer.StartTimer($"[{rid}] vote pull");
                        var voteSearch = new VoteSearch();
                        voteSearch.UserIds.Add(requester.userId);
                        voteSearch.ContentIds.AddRange(baseIds);
                        userVotes = await voteSource.SimpleSearchAsync(voteSearch);
                        services.timer.EndTimer(t);

                        t = services.timer.StartTimer($"[{rid}] vote aggregate");
                        votes = new Dictionary<string, Dictionary<long, SimpleAggregateData>>();
                        foreach (var voteWeight in Votes.VoteWeights)
                        {
                            votes.Add(voteWeight.Key, await voteSource.GroupAsync<EntityRelation, long>(
                                await voteSource.SearchIds(new VoteSearch() { ContentIds = baseIds, Vote = voteWeight.Key }), voteSource.PermIdSelector)); //x => x.entityId2));
                        }
                        services.timer.EndTimer(t);
                    }

                    baseResult.ForEach(x =>
                    {
                        if(watches != null)
                        {
                            if (watches.ContainsKey(x.id))
                                x.about.watches = watches[x.id];
                            x.about.watching = userWatching.Any(y => y.contentId == x.id);
                        }

                        if(comments != null)
                        {
                            if (comments.ContainsKey(x.id))
                                x.about.comments = comments[x.id];
                        }

                        if(votes != null)
                        {
                            x.about.myVote = userVotes.FirstOrDefault(y => y.contentId == x.id)?.vote;

                            foreach (var voteWeight in Votes.VoteWeights)
                            {
                                x.about.votes.Add(voteWeight.Key, new SimpleAggregateData());

                                if (votes[voteWeight.Key].ContainsKey(x.id))
                                    x.about.votes[voteWeight.Key] = votes[voteWeight.Key][x.id];
                            }
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
            else
            {
                //Eventually, about is going away anyway
                baseResult.ForEach(x =>
                {
                    x.about = null;
                });
            }

            cache.StoreItem(key, baseResult);
            return baseResult;
        }
    }
}