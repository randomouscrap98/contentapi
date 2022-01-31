using AutoMapper;

namespace contentapi.Search;

public class AboutSearchField
{
    /// <summary>
    /// These are fields which can be used in the "query" part of a request, ie queried against
    /// </summary>
    /// <value></value>
    public bool queryable {get;set;} = false;

    /// <summary>
    /// Whether this particular field is even pulled from the database or done within the api itself. It is RARE for
    /// a field to be computed...
    /// </summary>
    public bool computed {get;set;} = false;

    /// <summary>
    /// An estimated rating for how expensive this field is to pull. For additional clarity, fields which can't be pulled will have this set to -1
    /// </summary>
    /// <value></value>
    public int expensive {get;set;} = -1;

    public string type {get;set;} = "unknown";
}

public class AboutSearchFieldProfile : Profile
{
    public AboutSearchFieldProfile()
    {
        this.CreateMap<DbFieldInfo, AboutSearchField>()
            .ForMember(dest => dest.type, opt => opt.MapFrom(src => src.fieldType.Name));
    }
}