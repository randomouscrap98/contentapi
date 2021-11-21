using System.Collections.Generic;
using Randomous.EntitySystem;

namespace contentapi
{
    public class EntityRelationPackage
    {
        public EntityRelation Main;
        public List<EntityRelation> Related = new List<EntityRelation>();
    }

}