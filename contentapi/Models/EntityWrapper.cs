using System.Collections.Generic;
using Randomous.EntitySystem;

namespace contentapi.Models
{
    public class EntityWrapper : Entity
    {
        public List<EntityValue> Values = new List<EntityValue>();
        public List<EntityRelation> Relations = new List<EntityRelation>();

        public EntityWrapper() {}
        public EntityWrapper(Entity copy) : base(copy) {}
    }
}