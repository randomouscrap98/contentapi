using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class WatchViewService : BaseViewServices<WatchView, WatchSearch>, IViewService<WatchView, WatchSearch>
    {
        protected WatchViewSource converter;
        protected ContentViewService contentService;

        public WatchViewService(ViewServicePack services, ILogger<WatchViewService> logger, WatchViewSource converter,
            ContentViewService contentService) 
            : base(services, logger) 
        { 
            this.converter = converter;
            this.contentService = contentService;
        }

        public async Task<WatchView> DeleteAsync(long id, Requester requester)
        {
            var item = await converter.FindByIdRawAsync(id);

            if(item == null)
                throw new BadRequestException($"Can't find watch with id {id}");

            await provider.DeleteAsync(item);
            return converter.ToView(item);
        }

        public override Task<List<WatchView>> PreparedSearchAsync(WatchSearch search, Requester requester)
        {
            logger.LogTrace($"Watch SearchAsync called by {requester}");

            return converter.SimpleSearchAsync(search, q =>
                services.permissions.PermissionWhere(
                    q.Where(x => requester.system || x.relation.entityId1 == requester.userId), requester, Keys.ReadAction));
            //You can only get your own watches.
        }

        public async Task<WatchView> GetByContentId(long contentId, Requester requester)
        {
            var search = new WatchSearch();
            search.ContentIds.Add(contentId);

            var view = (await SearchAsync(search, requester)).OnlySingle();

            if(view == null)
                throw new NotFoundException($"No content found with id {contentId}");
            
            return view;
        }

        public async Task<WatchView> WriteAsync(WatchView view, Requester requester)
        {
            if(view.id != 0)
            {
                //Go get the existing view.
                var existing = await this.FindByIdAsync(view.id, requester);

                //Can only do your own watches
                if(existing == null || existing.userId != requester.userId)
                    throw new NotFoundException($"No watch with id {view.id}"); //More things need to be "not found"
                
                //When updating, we ONLY (ONLY) allow the last id to be updated.
                existing.lastNotificationId = view.lastNotificationId;
                view = existing;
            }
            else
            {
                //Always set the view's create date to now and the user to us
                view.createDate = DateTime.UtcNow;
                view.userId = requester.userId;

                //must not watch things you can't be on. in the unlikely event that people are watching something
                //that becomes readonly... oh well.

                //Can only watch content right now. Perhaps these checks should go in the controller? 
                //But why the arbitrary cut off? Some can do this, some can't, yadda yadda? Yes actually...
                //we need a requester for writing/deleting etc but it doesn't necessarily have to check.
                //Perhaps the checks should go so far out... yes, that can be next refactor
                //TODO: move permission checks out somewhere else
                var existingContent = await contentService.FindByIdAsync(view.contentId, requester);

                if (existingContent == null)
                    throw new BadRequestException($"There is no content with id {view.contentId}");

                //One last check: are we already watching thhis?
                var existingSearch = new WatchSearch();
                existingSearch.ContentIds.Add(view.contentId);
                existingSearch.UserIds.Add(view.userId);

                var existing = (await SearchAsync(existingSearch, requester)).OnlySingle();

                if (existing != null)
                    throw new BadRequestException($"Already watching {view.contentId}");

                //Kind of a hack: we know activity and all things must come from relations. So since we 
                //JUST watched it, the id will be... well, the last id
                view.lastNotificationId = await Q<EntityRelation>().MaxAsync(x => x.id);
            }

            var rel = converter.FromView(view);
            await provider.WriteAsync(rel);
            return converter.ToView(rel);
        }
    }
}