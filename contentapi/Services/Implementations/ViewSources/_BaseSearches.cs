using System.Collections.Generic;
using AutoMapper;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //Literally just a stand-in / abstraction away from the entity system. I don't know
    public class BaseSearch : EntitySearchBase, IConstrainedSearcher { }

    public class BaseParentSearch : BaseSearch
    {
        public List<long> ParentIds {get;set;} = new List<long>();
    }

    public class BaseContentSearch : BaseParentSearch
    {
        public string Name {get;set;}
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