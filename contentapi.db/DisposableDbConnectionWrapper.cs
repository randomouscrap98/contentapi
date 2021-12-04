using System;
using System.Data;

namespace contentapi.Db
{
    /// <summary>
    /// A simple wrapper which lets consumers of a dependency injected connection request particular connections STILL without
    /// knowing the implementation, since the connection is still "IDbConnection". Essentially: a "named" connection.
    /// </summary>
    public abstract class DisposableDbConnectionWrapper : IDisposable
    {
        //An auto-property dbconnection that's readonly. Autoproperties have an automatic backing
        //store variable, but allow it to be presented like a property
        public IDbConnection Connection {get;}
    
        public DisposableDbConnectionWrapper(IDbConnection connection)
        {
            Connection = connection;
        }

        public void Dispose()
        {
            Connection.Dispose();
        }
    }
}