using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace contentapi.trials
{
    /*public class Entity
    {
        public long id {get;set;}
        public string name {get;set;}
        public string content {get;set;}
        public DateTime createDate {get;set;}
        public string type {get;set;}
    }

    public class EntityRelation
    {
        public long id {get;set;}
        public long entityId1 {get;set;}
        public long entityId2 {get;set;}
        public string type {get;set;}
        public string value {get;set;}
        public DateTime createDate {get;set;}
    }

    public class EntityValue
    {
        public long id {get;set;}
        public long entityId {get;set;}
        public string key {get;set;}
        public string value{get;set;}
        public DateTime createDate {get;set;}
    }

    public class EntitySearchBase
    {
        public List<long> Ids = new List<long>();
        public DateTime CreateStart = new DateTime(0);
        public DateTime CreateEnd = new DateTime(0);
        //public bool CaseInsensitive = true;
    }

    public class EntitySearch:EntitySearchBase
    {
        public string TypeRegex = "";
        public string NameRegex = "";
        //public List<string> Types = new List<string>();
        //public List<string> Names = new List<string>();
    }

    public class EntityValueSearch : EntitySearchBase
    {
        public Dictionary<string, string> ValueRegex = new Dictionary<string, string>(); //string, List<string>> Values = new Dictionary<string, List<string>>();
        public List<long> EntityIds = new List<long>();
    }

    public class EntityRelationSearch : EntitySearchBase
    {
        public string TypeRegex;
        //public List<string> Types = new List<string>();
        public List<long> EntityIds1 = new List<long>();
        public List<long> EntityIds2 = new List<long>();
    }

    public interface IEntityProvider
    {
        Task<List<Entity>> GetEntitiesAsync(EntitySearch search);
        Task<List<EntityValue>> GetEntityValuesAsync(EntityValueSearch search);
        Task<List<EntityRelation>> GetEntityRelationsAsync(EntityRelationSearch search);

        Task WriteEntities(IEnumerable<Entity> entities);
        Task WriteEntityValues(IEnumerable<EntityValue> values);
        Task WriteEntityRelations(IEnumerable<EntityRelation> relations);
    }

    //TODO: DON'T NAME IT THIS
    public class EntityProviderHelper
    {
        void ApplyEntitySearch(IQueryable<Entity> query, EntitySearch search)
        {
            //TODO/WARN: REGEX might not work! Check performance: EFCore is supposed to support this, AND sqlite supports it!
            if(search.Ids.Count > 0)
                query = query.Where(x => search.Ids.Contains(x.id));

            if(!string.IsNullOrEmpty(search.NameRegex))
            {
                var regex = new Regex(search.NameRegex);
                query = query.Where(x => regex.IsMatch(x.name));
            } //search.Names.Count > 0)

            //{

                //if(search.CaseInsensitive)
                //{
                //    var lowerNames = search.Names.Select(x => x.ToLower());
                //    query = query.Where(x => lowerNames.Contains(x.name.ToLower()));
                //}
                //else
                //    query = query.Where(x => search.Names.Contains(x.name));
            //}
        }
    }*/
}