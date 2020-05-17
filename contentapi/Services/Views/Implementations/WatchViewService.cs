using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Services.Views.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Views.Implementations
{
    public class WatchViewService : BaseViewServices, IViewService<WatchView, WatchSearch>
    {
        protected WatchViewSource converter;

        public WatchViewService(ViewServicePack services, ILogger<BaseViewServices> logger, WatchViewSource converter) 
            : base(services, logger) 
        { 
            this.converter = converter;
        }

        public async Task<WatchView> DeleteAsync(long id, Requester requester)
        {
            var item = await converter.FindByIdRawAsync(id);

            if(item == null)
                throw new BadRequestException($"Can't find watch with id {id}");

            await provider.DeleteAsync(item);
            return converter.ToView(item);
        }

        public Task<List<WatchView>> SearchAsync(WatchSearch search, Requester requester)
        {
            logger.LogTrace($"Watch SearchAsync called by {requester}");

            return converter.SimpleSearchAsync(search, q =>
                services.permissions.PermissionWhere(
                    q.Where(x => requester.system || x.relation.entityId1 == requester.userId), requester, Keys.ReadAction));
            //You can only get your own watches.
        }

        public async Task<WatchView> WriteAsync(WatchView view, Requester requester)
        {
            //Just don't even allow views with ids
            if(view.id != 0)
                throw new BadRequestException("Can't edit watches! Only delete or insert!");

            //must not watch things you can't be on. in the unlikely event that people are watching something
            //that becomes readonly... oh well.
            var content = await provider.FindByIdAsync(view.contentId);

            if(content == null || !services.permissions.CanUser(requester, Keys.ReadAction, content))
                throw new BadRequestException($"There is no content with id {view.contentId}");

            //Now jsut write the releation
            await provider.WriteAsync(converter.FromView(view));

            return view;
        }
    }
}