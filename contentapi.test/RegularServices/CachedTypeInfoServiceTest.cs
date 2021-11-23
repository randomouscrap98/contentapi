using contentapi.Implementations;
using Xunit;

namespace contentapi.test;

public class CachedTypeInfoServiceTest : UnitTestBase
{
    protected CachedTypeInfoService service;

    public CachedTypeInfoServiceTest()
    {
        service = GetService<CachedTypeInfoService>();//new CachedTypeInfoService();
    }

    public class TestView
    {
        public long queryableFieldLong {get;set;}

        [Searchable]
        public string searchableFieldString {get;set;} = "";

        [Computed]
        public double computedField {get;}
    }

    [Fact] 
    public void GetUserType()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal(typeof(TestView), typeInfo.type);
        Assert.Contains("queryableFieldLong", typeInfo.queryableFields);
        Assert.DoesNotContain("queryableFieldLong", typeInfo.searchableFields);
        Assert.Contains("searchableFieldString", typeInfo.queryableFields);
        Assert.Contains("searchableFieldString", typeInfo.searchableFields);
        Assert.DoesNotContain("computedField", typeInfo.queryableFields);
        Assert.DoesNotContain("computedField", typeInfo.searchableFields);
    }
}