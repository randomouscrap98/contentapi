using System.Data;
using contentapi.Db;
using Dapper;
using Xunit;

namespace contentapi.test;

public class DbUnitTestBaseTest : DbUnitTestBase
{
    public DbUnitTestBaseTest() { }

    //public IDbConnection GetConnection()
    //{
    //    return GetService<>().Connection;
    //}
    
    [Fact]
    public void CheckIfUserTableExists()
    {
        using(var con = dbFactory.CreateRaw())
        {
            var users = con.Query<User>("select * from users");
            Assert.Empty(users);
        }
    }
}