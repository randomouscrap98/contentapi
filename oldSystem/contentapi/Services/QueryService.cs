using System;
using System.Linq;
using contentapi.Models;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Threading.Tasks;

namespace contentapi.Services
{
    //Query parameters (from url) for searching/querying collections
    public class CollectionQuery
    {
        public int offset {get;set;} = 0;
        public int count {get;set;} = 0;
        public string sort {get;set;} = "";
        public string order {get;set;} = "";
        public string ids { get;set;} = "";

        //For now, I don't know WHAT to do for these, so just don't include it
        //private bool deleted {get;set;} = false;
    }

    public class QueryService
    {
        //Counts and... stuff (for counting part of search)
        public int DefaultResultCount = 1000;
        public int MaxResultCount = 5000;

        //Sorting fields and stuff
        public string IdSort = "id";
        public string CreateSort = "create";

        //Ordering fields
        public string AscendingOrder = "asc";
        public string DescendingOrder = "desc";

        public QueryService()
        {
        }

        public IQueryable<W> ApplySort<W>(IQueryable<W> originSet, CollectionQuery query) where W : EntityChild
        {
            //This is AWFUL but... yeah, entity framework core. Good stuff?
            if(query.order == AscendingOrder)
            {
                if(query.sort == IdSort)
                    return originSet.OrderBy(x => x.Entity.id);
                else if(query.sort == CreateSort)
                    return originSet.OrderBy(x => x.Entity.createDate);
            }
            else if (query.order == DescendingOrder)
            {
                if(query.sort == IdSort)
                    return originSet.OrderByDescending(x => x.Entity.id);
                else if(query.sort == CreateSort)
                    return originSet.OrderByDescending(x => x.Entity.createDate);
            }
            else
            {
                throw new InvalidOperationException($"Unknown order type ({AscendingOrder}/{DescendingOrder})");
            }

            throw new InvalidOperationException($"Unknown sort type");
        }

        private void FixQuery(CollectionQuery query)
        {
            //Set some nice defaults for query parameters
            if(query.count <= 0)
                query.count = DefaultResultCount;

            if(query.count > MaxResultCount)
                throw new InvalidOperationException($"Too many objects! Max: {MaxResultCount}");

            if(string.IsNullOrWhiteSpace(query.sort))
                query.sort = CreateSort;

            if(string.IsNullOrWhiteSpace(query.order))
                query.order = AscendingOrder;

            query.order = query.order.ToLower();
            query.sort = query.sort.ToLower();
        }

        private List<long> ParseIds(CollectionQuery query)
        {
            List<long> ids = null;

            //One day, put a logger here!
            try { ids = query.ids.Split(",").Select(x => long.Parse(x)).ToList(); }
            catch { }

            return ids;
        }

        private IQueryable<T> ApplyPagination<T>(IQueryable<T> subset, CollectionQuery query)
        {
            try
            {
                return subset.Skip(query.offset).Take(query.count);
            }
            catch
            {
                throw new InvalidOperationException("Offset/count broke set; this is API laziness");
            }
        }

        public IQueryable<W> ApplyQuery<W>(IQueryable<W> originSet, CollectionQuery query) where W : EntityChild
        {
            FixQuery(query);
            var subSet = originSet;

            //For now, ALWAYS remove deleted entities. We'll figure out what to do later.
            //if(!query.deleted)
            subSet = subSet.Where(x => (x.Entity.status & EntityStatus.Deleted) == 0);

            var ids = ParseIds(query);

            if(ids != null && ids.Count > 0)
                subSet = subSet.Where(x => ids.Contains(x.entityId));

            //Will it always be ok to fail on bad sort?
            subSet = ApplySort(subSet, query);
            subSet = ApplyPagination(subSet, query);

            return subSet;
        }

        public IQueryable<W> ApplyEntityQuery<W>(IQueryable<W> originSet, CollectionQuery query) where W : Entity
        {
            FixQuery(query);
            var subSet = originSet;

            //For now, ALWAYS remove deleted entities. We'll figure out what to do later.
            //if(!query.deleted)
            subSet = subSet.Where(x => (x.status & EntityStatus.Deleted) == 0);

            var ids = ParseIds(query);

            if(ids != null && ids.Count > 0)
                subSet = subSet.Where(x => ids.Contains(x.id));

            //WARN: NO SORTING ON ENTITY QUERY YET
            //subSet = ApplySort(subSet, query);
            subSet = ApplyPagination(subSet, query);

            return subSet;
        }

        public async Task<W> GetSingleWithQueryAsync<W>(IQueryable<W> originSet, long id) where W : EntityChild
        {
            var query = new CollectionQuery() { ids = id.ToString() };
            return await ApplyQuery(originSet, query).FirstOrDefaultAsync();
        }

        public async Task<W> GetSingleEntityWithQueryAsync<W>(IQueryable<W> originSet, long id) where W : Entity
        {
            var query = new CollectionQuery() { ids = id.ToString() };
            return await ApplyEntityQuery(originSet, query).FirstOrDefaultAsync();
        }

        //How to RETURN items (the object we return... maybe make it a real class)
        public Dictionary<string, object> GetGenericCollectionResult<W>(IEnumerable<W> items, IEnumerable<string> links = null)
        {
            return new Dictionary<string, object>{ 
                { "collection" , items },
                { "_links",  links ?? new List<string>() }, //one day, turn this into HATEOS
                //_claims = User.Claims.ToDictionary(x => x.Type, x => x.Value)
            };
        }

        public IEnumerable<W> GetCollectionFromResult<W>(Dictionary<string, object> result)
        {
            return (IEnumerable<W>)result["collection"];
        }
    }
}