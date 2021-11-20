using System;
using System.Data;

namespace contentapi
{
    public class DisposableDbConnectionWrapper : IDisposable
    {
        public IDbConnection Connection {get;set;}

        public void Dispose()
        {
            Connection.Dispose();
        }
    }
}