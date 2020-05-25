using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class VoteViewService : BaseViewServices<VoteView, VoteSearch>, IViewService<VoteView, VoteSearch>
    {
        protected VoteViewSource converter;
        protected ContentViewService contentService;

        public VoteViewService(ViewServicePack services, ILogger<VoteViewService> logger, VoteViewSource converter,
            ContentViewService contentService) 
            : base(services, logger) 
        { 
            this.converter = converter;
            this.contentService = contentService;
        }

        public async Task<VoteView> DeleteAsync(long id, Requester requester)
        {
            var item = await converter.FindByIdRawAsync(id);

            if(item == null)
                throw new BadRequestException($"Can't find vote with id {id}");

            await provider.DeleteAsync(item);
            return converter.ToView(item);
        }

        public override Task<List<VoteView>> PreparedSearchAsync(VoteSearch search, Requester requester)
        {
            logger.LogTrace($"Vote SearchAsync called by {requester}");

            return converter.SimpleSearchAsync(search, q =>
                services.permissions.PermissionWhere(
                    q.Where(x => requester.system || x.relation.entityId1 == requester.userId), requester, Keys.ReadAction));
            //You can only get your own votes....
        }

        public async Task<VoteView> GetByContentId(long contentId, Requester requester)
        {
            var search = new VoteSearch();
            search.ContentIds.Add(contentId);

            var view = (await SearchAsync(search, requester)).OnlySingle();

            if(view == null)
                throw new NotFoundException($"No content found with id {contentId}");
            
            return view;
        }

        public async Task<VoteView> WriteAsync(VoteView view, Requester requester)
        {
            if(view.id != 0)
            {
                throw new BadRequestException("Don't send ID when re-voting!");
            }
            else
            {
                view.vote = view.vote?.ToLower();

                if(!Votes.VoteWeights.ContainsKey(view.vote))
                    throw new BadRequestException($"Vote not in {string.Join(",", Votes.VoteWeights.Keys)}");

                //Always set the view's create date to now and the user to us
                view.createDate = DateTime.UtcNow;
                view.userId = requester.userId;

                var existingContent = await contentService.FindByIdAsync(view.contentId, requester);

                if (existingContent == null)
                    throw new BadRequestException($"There is no content with id {view.contentId}");

                //One last check: have we already voted on this?
                var existingSearch = new VoteSearch();
                existingSearch.ContentIds.Add(view.contentId);
                existingSearch.UserIds.Add(view.userId);

                var existing = (await SearchAsync(existingSearch, requester)).OnlySingle();

                //Take over the old vote
                if (existing != null)
                    view.id = existing.id;
            }

            var rel = converter.FromView(view);
            await provider.WriteAsync(rel);
            return converter.ToView(rel);
        }
    }
}