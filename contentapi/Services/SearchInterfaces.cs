using System.Collections.Generic;

namespace contentapi.Services
{
    public interface IIdSearcher
    {
        List<long> Ids {get;set;}
    }
}