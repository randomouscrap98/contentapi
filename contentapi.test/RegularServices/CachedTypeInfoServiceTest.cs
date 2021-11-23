using contentapi.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test;

public class CachedTypeInfoServiceTest : UnitTestBase
{
    protected CachedTypeInfoService service;

    public CachedTypeInfoServiceTest()
    {
        service = GetService<CachedTypeInfoService>();//new CachedTypeInfoService();
    }

    [Fact] 
    public void GetUserType()
    {
        var typeInfo = service.GetTypeInfo<UserView>();
        Assert.Equal(typeof(UserView), typeInfo.type);
        Assert.Contains("id", typeInfo.searchableFields);
    }
}