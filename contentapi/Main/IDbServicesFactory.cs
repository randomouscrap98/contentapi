using System.Data;
using contentapi.Search;

namespace contentapi.Main;

/// <summary>
/// A service to create the services that have a special user-controlled lifetime, because all of these
/// hold onto a database connection for the duration of their lifetime. Note that ALL SERVICES CREATED
/// HERE SHOULD NEVER BE INJECTED DIRECTLY!! Only this interface!!
/// </summary>
public interface IDbServicesFactory
{
    IDbWriter CreateWriter();
    IGenericSearch CreateSearch();
    IDbConnection CreateRaw();
}