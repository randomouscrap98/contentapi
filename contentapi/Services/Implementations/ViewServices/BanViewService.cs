using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class BanViewBaseService<B> : BaseViewServices<B, BanSearch>, IViewService<B, BanSearch> where B : BanViewBase, new()
    {
        protected BanViewBaseSource<B> source;

        public BanViewBaseService(ViewServicePack services, ILogger<BanViewBaseService<B>> logger, BanViewBaseSource<B> source)
            : base(services, logger) 
        { 
            this.source = source;
        }

        public Task<B> DeleteAsync(long id, Requester requester)
        {
            throw new System.NotImplementedException(); //Yeah for real, can't delete bans
        }

        public override async Task<List<B>> PreparedSearchAsync(BanSearch search, Requester requester)
        {
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Can't read bans");

            return await source.SimpleSearchAsync(search, (q) => q);
        }

        public async Task<B> WriteAsync(B view, Requester requester)
        {
            if(!services.permissions.IsSuper(requester))
                throw new AuthorizationException("Can't write bans");

            view.id = 0;
            view.createDate = DateTime.Now;  //Ignore create date, it's always now
            view.createUserId = requester.userId;    //Always requester

            var relation = source.FromView(view);
            await services.provider.WriteAsync(relation);
            return source.ToView(relation);
        }
    }

    public class PublicBanViewService : BanViewBaseService<PublicBanView>
    {
        public PublicBanViewService(ViewServicePack services, ILogger<BanViewBaseService<PublicBanView>> logger, BanViewBaseSource<PublicBanView> source) 
            : base(services, logger, source) { }
    }
}