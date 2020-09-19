using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class BanSearch : BaseSearch
    {
        public List<long> BannedUserIds {get;set;} = new List<long>();
        public List<long> CreateUserIds {get;set;} = new List<long>();

        public DateTime ExpireDateStart {get;set;}
        public DateTime ExpireDateEnd {get;set;}
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

    public abstract class BanViewBaseSource<B> : BaseRelationViewSource<B, EntityRelation, EntityGroup, BanSearch> where B : BanViewBase, new()
    {
        public BanViewBaseSource(ILogger<BanViewBaseSource<B>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        //This shouldn't match much, permissions aren't assigned to users, and we don't care...?
        public override Expression<Func<EntityRelation, long>> PermIdSelector => x => x.entityId2;
        public string ExpireDateFormat = "s"; //"yyyy-MM-ddTHH:mm:ss";


        //public BanType RTypeToBType(string relationType)
        //{
        //    var part = relationType.Substring(EntityType.Length, SubtypeLength);

        //    if(part == Keys.BanPublicKey)
        //        return BanType.@public;
        //    else
        //        throw new InvalidOperationException("Couldn't convert ban (sub)type " + relationType);
        //}

        //public string BTypeToRSubType(BanType type)
        //{
        //    if(type == BanType.@public)
        //        return Keys.BanPublicKey;
        //    else
        //        throw new InvalidOperationException("Couldn't reverse convert ban (sub)type " + type);
        //}

        public override B ToView(EntityRelation relation)
        {
            var view = new B();

            view.id = relation.id;
            view.createDate = (DateTime)relation.createDateProper();
            view.createUserId = relation.entityId1;
            view.bannedUserId = relation.entityId2;
            //view.type = RTypeToBType(relation.type);
            view.message = relation.value;
            view.expireDate = DateTime.Parse(relation.type.Substring(EntityType.Length) + "Z");

            return view;
        }

        public override EntityRelation FromView(B view)
        {
            var relation = new EntityRelation();
            relation.entityId1 = view.createUserId;
            relation.entityId2 = view.bannedUserId;
            relation.createDate = view.createDate;
            relation.type = EntityType + view.expireDate.ToUniversalTime().ToString(ExpireDateFormat); //CONVERT TO UNIVERSAL!!
            relation.value = view.message;
            relation.id = view.id;
            return relation;
        }

        public override EntityRelationSearch CreateSearch(BanSearch search)
        {
            var es = base.CreateSearch(search);
            es.TypeLike += "%"; //((search.Type == BanType.none ? BTypeToRSubType(search.Type) : "") + "%");
            return es;
        }

        public override IQueryable<long> FinalizeQuery(IQueryable<EntityGroup> query, BanSearch search)  
        {
            if(search.ExpireDateStart.Ticks != 0)
                query = query.Where(x => String.Compare(x.relation.type, EntityType + search.ExpireDateStart.ToString(ExpireDateFormat)) >= 0);
            if(search.ExpireDateEnd.Ticks != 0)
                query = query.Where(x => String.Compare(x.relation.type, EntityType + search.ExpireDateEnd.ToString(ExpireDateFormat)) <= 0);

            return base.FinalizeQuery(query, search);
        }

        //We have this simple code everywhere because we may NOT return the same thing every time
        public override Task<List<EntityRelation>> RetrieveAsync(IQueryable<long> ids)
        {
            return provider.GetListAsync(GetByIds<EntityRelation>(ids));
        }
    }

    public class PublicBanViewBaseSource : BanViewBaseSource<PublicBanView>
    {
        public PublicBanViewBaseSource(ILogger<BanViewBaseSource<PublicBanView>> logger, IMapper mapper, IEntityProvider provider) 
            : base(logger, mapper, provider) { }

        public override string EntityType => Keys.PublicBanKey;
    }
    //BaseRelationViewSource<B, EntityRelation, EntityGroup, BanSearch> where B : BanViewBase, new()
}