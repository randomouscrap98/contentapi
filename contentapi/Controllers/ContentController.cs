using AutoMapper;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ContentSearch : EntitySearchBase
    {
        public string Title {get;set;}
        public string Keyword {get;set;}
    }

    public class ContentControllerProfile : Profile
    {
        public ContentControllerProfile()
        {
            CreateMap<ContentSearch, EntitySearch>().ForMember(x => x.NameLike, o => o.MapFrom(s => s.Title));
        }
    }

    public class ContentController : PermissionBaseController<ContentView>
    {
        public ContentController(ILogger<ContentController> logger, ControllerServices services)
            : base(services, logger) { }

        protected override string EntityType => keys.ContentType;
        protected override string ParentType => keys.CategoryType;
        
        protected override EntityPackage CreateBasePackage(ContentView view)
        {
            var package = NewEntity(view.title, view.content);

            //Need to add LOTS OF CRAP

            return package;
        }

        protected override ContentView CreateBaseView(EntityPackage package)
        {
            var view = new CategoryView();
            view.name = package.Entity.name;
            view.description = package.Entity.content;
            return null; //view;
        }
    }
}