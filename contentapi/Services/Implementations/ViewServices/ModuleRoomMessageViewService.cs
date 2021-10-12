using contentapi.Views;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    //Module room messages are literally just comments with a different type. As they can't be edited
    //or deleted, they are also not linked to anything else and thus are stored as a single relation
    public class ModuleRoomMessageViewService : CommentViewService
    {
        //WARN: uses the same cache as comments! if we get a lot of module room messages, they could push out the
        //comments, which are arguably more important!
        public ModuleRoomMessageViewService(ViewServicePack services, ILogger<CommentViewService> logger,
            ModuleRoomMessageViewSource converter, WatchViewSource watchSource, BanViewSource banSource, 
            ContentViewSource contentSource, ICodeTimer timer, CacheService<long, CommentView> singlecache) : 
            base(services, logger, converter, watchSource, banSource, contentSource, timer, singlecache) {}
    }

}