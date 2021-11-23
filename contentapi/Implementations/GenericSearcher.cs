using System.Data;
using contentapi.Db;

namespace contentapi.Implementations;

public class GenericSearcher : IGenericSearch
{
    protected ILogger logger;
    protected IDbConnection dbcon;
    protected ITypeInfoService typeService;

    //Should this be configurable? I don't care

    public GenericSearcher(ILogger<GenericSearcher> logger, ContentApiDbConnection connection,
        ITypeInfoService typeInfoService)
    {
        this.logger = logger;
        this.dbcon = connection.Connection;
        this.typeService = typeInfoService;
    }

    //All searches are reads, don't need to open the connection OR set up transactions, wow.
    public Task<Dictionary<string, object>> Search(SearchRequests requests)
    {
        
        throw new NotImplementedException();
    }
}