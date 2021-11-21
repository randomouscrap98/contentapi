using System.Threading.Tasks;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //A special type JUST to get a new cache for this fake service 
    public class SpecialModuleCacheService : CacheService<long, CommentView>
    {
        public SpecialModuleCacheService(ILogger<CacheService<long, CommentView>> logger, CacheServiceConfig config) : base(logger, config) { }
    }

    //Module room messages are literally just comments with a different type. As they can't be edited
    //or deleted, they are also not linked to anything else and thus are stored as a single relation
    public class ModuleRoomMessageViewService : CommentViewService
    {
        //WARN: uses the same cache as comments! if we get a lot of module room messages, they could push out the
        //comments, which are arguably more important!
        public ModuleRoomMessageViewService(ViewServicePack services, ILogger<CommentViewService> logger,
            ModuleRoomMessageViewSource converter, WatchViewSource watchSource, BanViewSource banSource, 
            ContentViewSource contentSource, ICodeTimer timer, SpecialModuleCacheService singlecache) : 
            base(services, logger, converter, watchSource, banSource, contentSource, timer, singlecache) {}

        public Task<EntityPackage> CanUserDoOnParent(long parentId, string action, Requester requester)
        {
            return FullParentCheckAsync(parentId, action, requester);
        }
    }

}