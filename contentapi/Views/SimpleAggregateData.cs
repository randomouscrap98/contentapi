using System;

namespace contentapi.Views
{
    public class SimpleAggregateData
    {
        public DateTime? firstDate {get;set;}
        public DateTime? lastDate {get;set;}
        public int count {get;set;}
    }

    //public class KeyedAggregateData : SimpleAggregateData
    //{
    //    public long key {get;set;}
    //}
}