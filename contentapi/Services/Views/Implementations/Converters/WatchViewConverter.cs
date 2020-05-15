using contentapi.Views;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class WatchViewConverter : BaseViewConverter, IViewConverter<WatchView, EntityRelation>
    {
        public EntityRelation FromView(WatchView view)
        {
            var relation = new EntityRelation()
            {
                value = view.lastNotificationId.ToString(),
                entityId1 = view.userId,
                entityId2 = -view.contentId
            };

            ApplyFromBaseView(view, relation);

            return relation;
        }

        public WatchView ToView(EntityRelation basic)
        {
            var view = new WatchView()
            {
                lastNotificationId = long.Parse(basic.value),
                userId = basic.entityId1,
                contentId = -basic.entityId2
            };

            ApplyToBaseView(basic, view);

            return view;
        }
    }
}