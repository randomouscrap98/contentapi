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
        public string Name {get;set;}
        public string AssociatedKey {get;set;}
        public string AssociatedValue {get;set;}
    }

    //The profile for all searches
    public class BaseSearchProfile : Profile
    {
        public BaseSearchProfile()
        {
            CreateMap<BaseContentSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Name))
                .IncludeAllDerived();
        }
    }
}