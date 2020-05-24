using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace contentapi.Views
{
    public class VoteData
    {
        public int vote {get;set;}
        public DateTime? date {get;set;}
    }

    public class AggregateVoteData
    {
        public int up {get;set;}
        public int down {get;set;}
        public Dictionary<string, VoteData> @public {get;set;}
    }

    //public class ContentViewFull : ContentView
    //{
    //    public List<VoteData> rawVotes {get;set;}
    //}

    public class ContentView : StandardView
    {
        [Required]
        [StringLength(128, MinimumLength=1)]
        public string name {get;set;}

        [Required]
        [StringLength(65536, MinimumLength = 2)]
        public string content {get;set;}

        public string type {get;set;}

        public List<string> keywords {get;set;} = new List<string>();

        protected override bool EqualsSelf(object obj)
        {
            var o = (ContentView)obj;
            return base.EqualsSelf(obj) && o.keywords.OrderBy(x => x).SequenceEqual(keywords.OrderBy(x => x));
        }

        public SimpleAggregateData comments {get;set;} = new SimpleAggregateData();
        public SimpleAggregateData watches {get;set;} = new SimpleAggregateData();

        public AggregateVoteData votes {get;set;} = new AggregateVoteData(); //Always have at least an empty vote data
    }
}