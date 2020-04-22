using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class BaseParentSearch : EntitySearchBase 
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