using System.Data;
using contentapi.Search;

namespace contentapi.Main;

public interface IDbServicesFactory
{
    IDbWriter CreateWriter();
    IGenericSearch CreateSearch();
    IDbConnection CreateRaw();
}