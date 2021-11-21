using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    //Module room messages are literally just comments with a different type. As they can't be edited
    //or deleted, they are also not linked to anything else and thus are stored as a single relation
    public class ModuleRoomMessageViewSource : CommentViewSource
    {
        public override string EntityType => Keys.ModuleHack;

        public ModuleRoomMessageViewSource(ILogger<CommentViewSource> logger, BaseViewSourceServices services)
            : base(logger, services) {}

        public override Task<List<EntityRelationPackage>> LinkAsync(List<EntityRelation> relations)
        {
            return Task.FromResult(relations.Select(x => new EntityRelationPackage()
            {
                Main = x,
                Related = new List<EntityRelation>()
            }).ToList());
        }
    }

}