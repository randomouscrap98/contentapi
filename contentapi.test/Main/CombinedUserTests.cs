using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.test.Mock;
using Xunit;

namespace contentapi.test;

public class CombinedUserTests : ViewUnitTestBase, IClassFixture<DbUnitTestSearchFixture>
{
    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected IDbWriter writer;
    protected IGenericSearch searcher;
    protected IUserService service;

    public CombinedUserTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();

        searcher = fixture.GetService<IGenericSearch>();
        writer = fixture.GetService<IDbWriter>();
        service = fixture.GetService<IUserService>();

        //UserService(fixture.GetService<ILogger<UserService>>(), searcher, fixture.GetService<IHashService>(), 
        //    fixture.GetService<IAuthTokenService<long>>(), config, fixture.GetService<ContentApiDbConnection>());

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public void DeletedUserNoLogin()
    {

    }
}