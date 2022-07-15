using System.Data;
using contentapi.Search;

namespace contentapi.Main;

public class ConfigurableDbServicesFactory : IDbServicesFactory
{
    public Func<IDbConnection> DbConnectionCreator {get;set;} = () => throw new NotImplementedException();
    public Func<IGenericSearch> GenericSearchCreator {get;set;} = () => throw new NotImplementedException();
    public Func<IDbWriter> DbWriterCreator {get;set;} = () => throw new NotImplementedException();
    //public Func<IUserService> UserServiceCreator {get;set;} = () => throw new NotImplementedException();

    public IDbConnection CreateRaw() => DbConnectionCreator();
    public IGenericSearch CreateSearch() => GenericSearchCreator();
    public IDbWriter CreateWriter() => DbWriterCreator();
    //public IUserService CreateUserService() => UserServiceCreator();
}