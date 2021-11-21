using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace contentapi.Services
{
    public interface IIdSearcher
    {
        List<long> Ids {get;set;}
    }

    public interface IConstrainedSearcher : IIdSearcher
    {
        int Limit {get;set;}
    }
}