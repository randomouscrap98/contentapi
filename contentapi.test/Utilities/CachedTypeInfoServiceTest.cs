using System.Collections.Generic;
using System.Linq;
using contentapi.Search;
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

        [ReadOnly]
        public List<string> readonlyField {get;set;} = new List<string>();
    }

    [Fact] 
    public void GetTypeInfoType()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal(typeof(TestView), typeInfo.type);
    }

    protected IEnumerable<string> RetrievableFields(DbTypeInfo info) => info.fields.Keys;
    //Where(x => x.Value.retrievable).Select(x => x.Key);

    protected IEnumerable<string> QueryableFields(DbTypeInfo info) => 
        info.fields.Where(x => x.Value.queryable).Select(x => x.Key);

    [Fact]
    public void GetTypeInfoQueryable()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Contains("queryableFieldLong", RetrievableFields(typeInfo));
        Assert.DoesNotContain("queryableFieldLong", QueryableFields(typeInfo)); //.searchableFields);
        Assert.False(typeInfo.fields["queryableFieldLong"].readOnly);
    }

    [Fact]
    public void GetTypeInfoSearchable()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Contains("searchableFieldString", RetrievableFields(typeInfo));
        Assert.Contains("searchableFieldString", QueryableFields(typeInfo));
        Assert.False(typeInfo.fields["searchableFieldString"].readOnly);
    }

    [Fact]
    public void GetTypeInfoComputed()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.DoesNotContain("computedField", RetrievableFields(typeInfo));
        Assert.DoesNotContain("computedField", QueryableFields(typeInfo));

        //For NOW, computed does not immediately imply readonly
        Assert.False(typeInfo.fields["computedField"].readOnly);
    }

    [Fact]
    public void GetTypeInfoReadonly()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.True(typeInfo.fields["readonlyField"].readOnly);
    }

    [Fact]
    public void GetTypeInfoFromField()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        //Assert.Single(typeInfo.fieldRemap.Keys);
        //Assert.Contains("remappedField", typeInfo.fieldRemap.Keys);
        Assert.Equal("otherField", typeInfo.fields["remappedField"].realDbColumn); //["remappedField"]);
    }

    [Fact] //Ensures fields with no FromField attribute default to their actual field name
    public void GetTypeInfoFromField_Default()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        //Assert.Single(typeInfo.fieldRemap.Keys);
        //Assert.Contains("remappedField", typeInfo.fieldRemap.Keys);
        Assert.Equal("searchableFieldString", typeInfo.fields["searchableFieldString"].realDbColumn); //["remappedField"]);
    }

    [Fact]
    public void GetTypeInfoDbObject() //Get the typeinfo for a DB object, which we still expect to fill in various fields
    {
        var typeInfo = service.GetTypeInfo<Db.ContentPermission>();
        Assert.Equal("content_permissions", typeInfo.modelTable); //Ofc, if you change the database name, change this here too
        //Assert.Single(typeInfo.fieldRemap.Keys);
        //Assert.Contains("remappedField", typeInfo.fieldRemap.Keys);
        //Assert.Equal("otherField", typeInfo.fieldRemap["remappedField"]);
    }
}