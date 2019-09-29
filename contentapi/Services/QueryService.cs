using System;
using System.Linq;
using contentapi.Models;
using System.Collections.Generic;

namespace contentapi.Services
{
    //Query parameters (from url) for searching/querying collections
    public class CollectionQuery
    {
        public int offset {get;set;} = 0;
        public int count {get;set;} = 0;
        public string sort {get;set;} = "";
        public string order {get;set;} = "";
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

        public Dictionary<string, Func<GenericModel, object>> Sorters; 

        public QueryService()
        {
            Sorters = new Dictionary<string, Func<GenericModel, object>>()
            {
                { IdSort, (x) => x.id },
                { CreateSort, (x) => x.createDate }
            };
        }

        public virtual System.Linq.Expressions.Expression<Func<W, object>> GetSorter<W>(string sort) where W : GenericModel 
        {
            if(Sorters.ContainsKey(sort))
                return (x) => Sorters[sort].Invoke(x);

            return null;
        }

        public IQueryable<W> ApplyQuery<W>(IQueryable<W> originSet, CollectionQuery query) where W : GenericModel
        {
            //Set some nice defaults for query parameters
            if(query.count <= 0)
                query.count = DefaultResultCount;

            if(query.count > MaxResultCount)
                throw new InvalidOperationException($"Too many objects! Max: {MaxResultCount}");
                //ThrowAction(BadRequest($"Too many objects! Max: {MaxResultCount}"));

            if(string.IsNullOrWhiteSpace(query.sort))
                query.sort = CreateSort;

            var order = query.order.ToLower();
            IQueryable<W> orderedSet = originSet;
            System.Linq.Expressions.Expression<Func<W, object>> sorter = GetSorter<W>(query.sort);

            if(sorter != null)
            {
                if (string.IsNullOrWhiteSpace(order) || order == AscendingOrder)
                    orderedSet = orderedSet.OrderBy(sorter);
                else if (order == DescendingOrder)
                    orderedSet = orderedSet.OrderByDescending(sorter);
                else
                    throw new InvalidOperationException($"Unknown order type ({AscendingOrder}/{DescendingOrder})");
            }

            IQueryable<W> slicedSet = orderedSet;

            try
            {
                slicedSet = slicedSet.Skip(query.offset).Take(query.count);
            }
            catch
            {
                throw new InvalidOperationException("Offset/count broke set; this is API laziness");
            }

            return slicedSet;
        }
    }
}