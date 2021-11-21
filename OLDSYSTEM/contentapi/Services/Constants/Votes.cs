
using System.Collections.Generic;
using System.Linq;

namespace contentapi.Services.Constants
{
    public static class Votes
    {
        public static readonly Dictionary<string, double> VoteWeights = new Dictionary<string, double>()
        {
            { "b", -1 },
            { "o", 0.35 },
            { "g", 1 }
        };
    }
}