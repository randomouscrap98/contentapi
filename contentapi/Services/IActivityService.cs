using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services
{
    public interface IActivityService
    {
        EntityRelation MakeActivity(EntityPackage package, long user, string action, string extra = null);

        //TODO: Stop using views, use models or something else.
        ActivityView ConvertToView(EntityRelation relation);
    }
}