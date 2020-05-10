using System;
using contentapi.Services.Constants;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ActivityService : IActivityService
    {
        protected ILogger logger;

        public ActivityService(ILogger<ActivityService> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Produce an activity for the given entity and action. Can include ONE piece of extra data.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="action"></param>
        /// <param name="extra"></param>
        /// <returns></returns>
        public EntityRelation MakeActivity(Entity entity, long user, string action, string extra = null)
        {
            var activity = new EntityRelation();
            activity.entityId1 = user;
            activity.entityId2 = -entity.id; //It has to be NEGATIVE because we don't want them linked to content
            activity.createDate = DateTime.Now;
            activity.type = Keys.ActivityKey + entity.type;
            activity.value = action;

            if(!string.IsNullOrWhiteSpace(extra))
                activity.value += extra;

            return activity;
        }

        public ActivityView ConvertToView(EntityRelation relation)
        {
            var view = new ActivityView();

            view.id = relation.id;
            view.date = (DateTime)relation.createDateProper();
            view.userId = relation.entityId1;
            view.contentId = -relation.entityId2;
            view.contentType = relation.type.Substring(Keys.ActivityKey.Length); // + keys.ContentType.Length);
            view.action = relation.value.Substring(1, 1); //Assume it's 1 character
            view.extra = relation.value.Substring(Keys.CreateAction.Length);

            return view;
        }

    }
}