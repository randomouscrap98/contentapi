using System.Data;
using contentapi.Db;
using Dapper;
using Xunit;

namespace contentapi.test;

public class DbUnitTestBaseTest : DbUnitTestBase
{
    public DbUnitTestBaseTest() { }

    public IDbConnection GetConnection()
    {
        return GetService<ContentApiDbConnection>().Connection;
    }
    
    [Fact]
    public void CheckIfUserTableExists()
    {
        using(var con = GetConnection())
        {
            var users = con.Query<User>("select * from users");
            Assert.Empty(users);
        }
    }
}