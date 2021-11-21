using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Db;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
//using contentapi.Services.Extensions

namespace contentapi.Services.Implementations
{
    public class BanSearch : BaseSearch
    {
        public List<long> BannedUserIds {get;set;} = new List<long>();
        public List<long> CreateUserIds {get;set;} = new List<long>();

        //public DateTime ExpireDateStart {get;set;}
        //public DateTime ExpireDateEnd {get;set;}
    }

    public class BanViewSourceProfile: Profile
    {
        public BanViewSourceProfile() 
        {
            CreateMap<BanSearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.CreateUserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.BannedUserIds.ToList()));
        }
    }

    public class BanViewSource : BaseRelationViewSource<BanView, EntityRelation, EntityGroup, BanSearch> //where B : BanViewBase, new()
    {
        public BanViewSource(ILogger<BanViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) { }

        //This shouldn't match much, permissions aren't assigned to users, and we don't care...?
        public override string EntityType => Keys.BanKey;
        public int SubtypeLength => Keys.BanPublicKey.Length;

        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => x.entityId2;
        public string ExpireDateFormat = "s";

        public BanType RTypeToBType(string type)
        {
            var t = type.Substring(EntityType.Length, SubtypeLength);

            if(t == Keys.BanPublicKey)
                return BanType.@public;
            else
                return BanType.none;
        }

        public string BTypeToRSubtype(BanType type)
        {
            if(type == BanType.@public)
                return Keys.BanPublicKey;
            else
                return "?";
        }

        public override BanView ToView(EntityRelation relation)
        {
            var view = new BanView();

            view.id = relation.id;
            view.createDate = (DateTime)relation.createDateProper();
            view.createUserId = relation.entityId1;
            view.bannedUserId = relation.entityId2;
            view.message = relation.value;
            view.type = RTypeToBType(relation.type);
            view.expireDate = DateTime.Parse(relation.type.Substring(EntityType.Length + SubtypeLength) + "Z");

            return view;
        }

        public override EntityRelation FromView(BanView view)
        {
            var relation = new EntityRelation();
            relation.entityId1 = view.createUserId;
            relation.entityId2 = view.bannedUserId;
            relation.createDate = view.createDate;
            relation.type = EntityType + BTypeToRSubtype(view.type) + view.expireDate.ToUniversalTime().ToString(ExpireDateFormat); //CONVERT TO UNIVERSAL!!
            relation.value = view.message;
            relation.id = view.id;
            return relation;
        }

        public override EntityRelationSearch CreateSearch(BanSearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += "%";
            return es;
        }

        public override Task<IQueryable<long>> FinalizeQuery(IQueryable<EntityGroup> query, BanSearch search)  
        {
            return base.FinalizeQuery(query, search);
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public override async Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return await services.provider.GetListAsync(await GetByIds<EntityRelation>(ids));
        }

        public BanView GetCurrentBan(IEnumerable<EntityRelation> relations)
        {
            var lastBan = relations.Where(x => x.type.StartsWith(Keys.BanKey)).OrderByDescending(x => x.id).FirstOrDefault();

            if(lastBan != null)
            {
                var result = ToView(lastBan);
                if(result.expireDate > DateTime.Now)
                    return result;
            }

            return null;
        }

        public async Task<BanView> GetUserBan(long uid)
        {
            var bansearch = new BanSearch();
            bansearch.BannedUserIds.Add(uid);
            return GetCurrentBan(await this.SimpleSearchRawAsync(bansearch));//this.SimpleSearchRawAsync(bansearch));
        }
    }

    //public class PublicBanViewSource : BanViewBaseSource<PublicBanView>
    //{
    //    public PublicBanViewSource(ILogger<BanViewBaseSource<PublicBanView>> logger, IMapper mapper, IEntityProvider provider) 
    //        : base(logger, mapper, provider) { }

    //    public override string EntityType => Keys.PublicBanKey;
    //}
}