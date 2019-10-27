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

        public IQueryable<W> ApplyQuery<W>(IQueryable<W> originSet, CollectionQuery query) where W : EntityChild
        {
            //Set some nice defaults for query parameters
            if(query.count <= 0)
                query.count = DefaultResultCount;

            if(query.count > MaxResultCount)
                throw new InvalidOperationException($"Too many objects! Max: {MaxResultCount}");

            if(string.IsNullOrWhiteSpace(query.sort))
                query.sort = CreateSort;
            
            var subSet = originSet;

            //For now, ALWAYS remove deleted entities. We'll figure out what to do later.
            //if(!query.deleted)
            subSet = subSet.Where(x => (x.Entity.status & EntityStatus.Deleted) == 0);

            List<long> ids = null;

            //One day, put a logger here!
            try { ids = query.ids.Split(",").Select(x => long.Parse(x)).ToList(); }
            catch { }

            if(ids != null && ids.Count > 0)
                subSet = subSet.Where(x => ids.Contains(x.entityId));

            var order = query.order.ToLower();

            try
            {
                subSet = ApplySort(subSet, query);
            }
            catch //(Exception ex)
            {
                //Put logging here! it's ok if the search didnt' work but maybe tell the user somehow!
            }

            try
            {
                subSet = subSet.Skip(query.offset).Take(query.count);
            }
            catch
            {
                throw new InvalidOperationException("Offset/count broke set; this is API laziness");
            }

            return subSet;
        }

        public async Task<W> GetSingleWithQueryAsync<W>(IQueryable<W> originSet, long id) where W : EntityChild
        {
            var query = new CollectionQuery() { ids = id.ToString() };
            return await ApplyQuery(originSet, query).FirstOrDefaultAsync();
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