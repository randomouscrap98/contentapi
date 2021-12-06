using AutoMapper;

namespace contentapi.Views;

public class FileViewProfile : Profile
{
    public FileViewProfile()
    {
        CreateMap<ContentView, FileView>().ReverseMap();
    }
}

public class ModuleViewProfile : Profile
{
    public ModuleViewProfile()
    {
        CreateMap<ContentView, ModuleView>().ReverseMap();
    }
}

public class PageViewProfile : Profile
{
    public PageViewProfile()
    {
        CreateMap<ContentView, PageView>().ReverseMap();
    }
}