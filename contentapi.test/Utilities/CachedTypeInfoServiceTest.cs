using contentapi.Search;
using contentapi.Utilities;
using Xunit;

namespace contentapi.test;

public class CachedTypeInfoServiceTest : UnitTestBase
{
    protected CacheDbTypeInfoService service;

    public CachedTypeInfoServiceTest()
    {
        service = GetService<CacheDbTypeInfoService>();//new CachedTypeInfoService();
    }

    public class TestView
    {
        public long queryableFieldLong {get;set;}

        [Searchable]
        public string searchableFieldString {get;set;} = "";

        [Computed]
        public double computedField {get;}

        [FromField("otherField")]
        public string? remappedField {get;set;}
    }

    [Fact] 
    public void GetTypeInfoType()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal(typeof(TestView), typeInfo.type);
    }

    [Fact]
    public void GetTypeInfoQueryable()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Contains("queryableFieldLong", typeInfo.queryableFields);
        Assert.DoesNotContain("queryableFieldLong", typeInfo.searchableFields);
    }

    [Fact]
    public void GetTypeInfoSearchable()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Contains("searchableFieldString", typeInfo.queryableFields);
        Assert.Contains("searchableFieldString", typeInfo.searchableFields);
    }

    [Fact]
    public void GetTypeInfoComputed()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.DoesNotContain("computedField", typeInfo.queryableFields);
        Assert.DoesNotContain("computedField", typeInfo.searchableFields);
    }

    [Fact]
    public void GetTypeInfoFromField()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Single(typeInfo.fieldRemap.Keys);
        Assert.Contains("remappedField", typeInfo.fieldRemap.Keys);
        Assert.Equal("otherField", typeInfo.fieldRemap["remappedField"]);
    }

    [Fact]
    public void GetTypeInfoDbObject() //Get the typeinfo for a DB object, which we still expect to fill in various fields
    {
        var typeInfo = service.GetTypeInfo<Db.ContentPermission>();
        Assert.Equal("content_permissions", typeInfo.table); //Ofc, if you change the database name, change this here too
        //Assert.Single(typeInfo.fieldRemap.Keys);
        //Assert.Contains("remappedField", typeInfo.fieldRemap.Keys);
        //Assert.Equal("otherField", typeInfo.fieldRemap["remappedField"]);
    }
}