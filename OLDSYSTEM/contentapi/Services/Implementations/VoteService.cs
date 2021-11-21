//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using contentapi.Services.Constants;
//using contentapi.Views;
//using Microsoft.Extensions.Logging;
//using Randomous.EntitySystem;
//
//namespace contentapi.Services.Implementations
//{
//    public enum ContentVote
//    {
//        None = 0,
//        Great = 2,
//        Ok = 1,
//        Bad = -1
//    }
//
//    public class VoteService
//    {
//        protected ILogger logger;
//        protected IEntityProvider provider;
//
//        public VoteService(ILogger<VoteService> logger, IEntityProvider provider)
//        {
//            this.logger = logger;
//            this.provider = provider;
//        }
//
//        public readonly Dictionary<string, ContentVote> VoteMapping = new Dictionary<string, ContentVote>()
//        {
//            { Keys.VoteBadRelation, ContentVote.Bad },
//            { Keys.VoteOkRelation, ContentVote.Ok },
//            { Keys.VoteGreatRelation, ContentVote.Great }
//        };
//
//        public Task<List<EntityRelation>> GetVotes(long contentId, long userId = -1)
//        {
//            return GetVotes(new [] {contentId}, userId);
//        }
//
//        public Task<List<EntityRelation>> GetVotes(IEnumerable<long> contentIds, long userId = -1)
//        {
//            var oldSearch = new EntityRelationSearch();
//            oldSearch.EntityIds2.AddRange(contentIds.Select(x => -x));
//            oldSearch.TypeLike = Keys.VoteRelation + "%";
//
//            if(userId > 0)
//                oldSearch.EntityIds1.Add(userId);
//
//            return provider.GetEntityRelationsAsync(oldSearch);
//        }
//
//        public EntityRelation CreateVote(long userId, long contentId, ContentVote voteType)
//        {
//            return new EntityRelation()
//            {
//                entityId1 = userId,
//                entityId2 = -contentId,
//                createDate = DateTime.UtcNow,
//                type = VoteMapping.First(x => x.Value == voteType).Key
//            };
//        }
//
//        public Dictionary<long, VoteData> ConvertVotes(IEnumerable<EntityRelation> relations)
//        {
//            return relations.ToDictionary(x => x.entityId1, y => new VoteData() { vote = y.type.Substring(Keys.VoteRelation.Length), date = y.createDateProper() });
//        }
//    }
//}