using System.Collections.Generic;
using AutoMapper;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //Literally just a stand-in / abstraction away from the entity system. I don't know
    public class BaseSearch : EntitySearchBase, IConstrainedSearcher { }

    public class BaseHistorySearch : BaseSearch
    {
        public List<long> CreateUserIds {get;set;} = new List<long>();
        public List<long> EditUserIds {get;set;} = new List<long>();
    }

    public class BaseParentSearch : BaseHistorySearch
    {
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class BaseContentSearch : BaseParentSearch
    {
        public string NameLike {get;set;}
        public List<string> Names {get;set;} = new List<string>();
        public string AssociatedKey {get;set;}
        public string AssociatedValue {get;set;}
    }

    //The profile for all searches
    public class BaseSearchProfile : Profile
    {
        public BaseSearchProfile()
        {
            CreateMap<BaseContentSearch, EntitySearch>()
                .ForMember(x => x.NameLike, o => o.MapFrom(s => s.NameLike))
                .ForMember(x => x.Names, o => o.MapFrom(s => s.Names))
                .IncludeAllDerived();
        }
    }

    public class IdLimit
    {
        public long id {get;set;}
        public long min {get;set;}
    }

    public class IdLimiter
    {
        public bool Watches {get;set;}
        public List<IdLimit> Limit {get;set;} = new List<IdLimit>();
    }
}