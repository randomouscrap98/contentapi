using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    public class SimpleAggregateData : CompareBase
    {
        public DateTime? firstDate {get;set;}
        public DateTime? lastDate {get;set;}
        public long lastId {get;set;}
        public int count {get;set;}
    }

    public class StandardAggregateData : SimpleAggregateData
    {
        public List<long> userIds {get;set;} = new List<long>();
    }
    //public class KeyedAggregateData : SimpleAggregateData
    //{
    //    public long key {get;set;}
    //}
}