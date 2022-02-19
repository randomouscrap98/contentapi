using System;
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

    [ResultFor(RequestType.file)]
    [SelectFrom("cows as c")]
    public class TestView
    {
        //An unmarked field is considered queryable, readonly, and NOT pullable by the query builder
        public long queryableFieldLong {get;set;}

        [NoQuery] //No query means you can't use this in a query... obviously
        public string unsearchableField {get;set;} = "";

        [FieldSelect]
        public double buildableField {get;set;}

        [FieldSelect("otherField")] //NOTE: the "as fieldname" is automatically appended... do we want that?
        public string? remappedField {get;set;}

        [Writable] //Anything marked "writable" should be freely writable
        public string freeWriteField {get;set;} = "";

        [Writable(WriteRule.AutoDate, WriteRule.Preserve)]
        public DateTime createDateField {get;set;}

        [Writable(WriteRule.Preserve, WriteRule.AutoUserId)]
        public DateTime editUserField {get;set;}

        [Multiline]
        public string multilineField {get;set;} = "wow\nyeah";

        [Expensive(3)]
        public int expensiveField {get;set;}
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

    ////Our new defaults are: unwritable, but queryable
    //protected void AssertDefaults(DbTypeInfo typeInfo, string name)
    //{
    //    Assert.True(typeInfo.fields[name].queryable);
    //    Assert.Null(typeInfo.fields[name].onInsert);
    //    Assert.Null(typeInfo.fields[name].onUpdate);
    //    Assert.False(typeInfo.fields[name].multiline);
    //}

    [Theory]
    [InlineData("queryableFieldLong", true, null, null, null, false, false, 0)]
    [InlineData("unsearchableField", false, null, null, null, false, false, 0)]
    [InlineData("buildableField", true, null, null, "buildableField", false, true, 0)]
    [InlineData("remappedField", true, null, null, "otherField", false, true, 0)]
    [InlineData("freeWriteField", true, WriteRule.User, WriteRule.User, null, false, false, 0)]
    [InlineData("createDateField", true, WriteRule.AutoDate, WriteRule.Preserve, null, false, false, 0)]
    [InlineData("editUserField", true, WriteRule.Preserve, WriteRule.AutoUserId, null, false, false, 0)]
    [InlineData("multilineField", true, null, null, null, true, false, 0)]
    [InlineData("expensiveField", true, null, null, null, false, false, 3)]
    public void GetTypeInfo_All(string fname, bool queryable, WriteRule? onInsert, WriteRule? onUpdate,
        string? fieldSelect, bool multiline, bool queryBuildable, int expensive)
    {
        var typeInfo = service.GetTypeInfo<TestView>();

        //ALL fields actually gettable should be retrievable
        Assert.Contains(fname, RetrievableFields(typeInfo));
        //Assert.Contains(fname, QueryableFields(typeInfo)); //.searchableFields);

        var field = typeInfo.fields[fname];

        //These are defaults, the field is empty
        Assert.Equal(queryable, field.queryable);
        Assert.Equal(onInsert, field.onInsert);
        Assert.Equal(onUpdate, field.onUpdate);
        Assert.Equal(fieldSelect, field.fieldSelect);
        Assert.Equal(multiline, field.multiline);
        Assert.Equal(queryBuildable, field.queryBuildable);
        Assert.Equal(expensive, field.expensive);
        //Assert.True(field.queryable);
        //Assert.Null(field.onInsert);
        //Assert.Null(field.onUpdate);
        //Assert.Null(field.fieldSelect);
        //Assert.False(field.multiline);
        //Assert.False(field.queryBuildable);
        //Assert.Equal(0, field.expensive);
    }

    //[Fact]
    //public void GetTypeInfoUnSearchable()
    //{
    //    const string fname = "unsearchableField";

    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Contains(fname, RetrievableFields(typeInfo));
    //    Assert.DoesNotContain(fname, QueryableFields(typeInfo));

    //    var field = typeInfo.fields[fname];

    //    Assert.False(field.queryable);
    //    Assert.Null(field.onInsert);
    //    Assert.Null(field.onUpdate);
    //    Assert.Null(field.fieldSelect);
    //    Assert.False(field.multiline);
    //    Assert.False(field.queryBuildable);
    //    Assert.Equal(0, field.expensive);
    //}

    //[Fact]
    //public void GetTypeInfoBuildable()
    //{
    //    const string fname = "remappedField";

    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Contains(fname, RetrievableFields(typeInfo));
    //    Assert.Contains(fname, QueryableFields(typeInfo));

    //    var field = typeInfo.fields[fname];

    //    Assert.True(field.queryable);
    //    Assert.Null(field.onInsert);
    //    Assert.Null(field.onUpdate);
    //    Assert.Equal(fname, field.fieldSelect);
    //    Assert.False(field.multiline);
    //    Assert.True(field.queryBuildable);
    //    Assert.Equal(0, field.expensive);
    //}

    //[Fact]
    //public void GetTypeInfoRemapped()
    //{
    //    const string fname = "buildableField";

    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Contains(fname, RetrievableFields(typeInfo));
    //    Assert.Contains(fname, QueryableFields(typeInfo));

    //    var field = typeInfo.fields[fname];

    //    //These are defaults, the field is empty
    //    Assert.True(field.queryable);
    //    Assert.Null(field.onInsert);
    //    Assert.Null(field.onUpdate);
    //    Assert.Equal("otherField", field.fieldSelect);
    //    Assert.False(field.multiline);
    //    Assert.True(field.queryBuildable);
    //    Assert.Equal(0, field.expensive);
    //}

    //[Fact]
    //public void GetTypeInfoWritable()
    //{
    //    const string fname = "writableField";

    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Contains(fname, RetrievableFields(typeInfo));
    //    Assert.Contains(fname, QueryableFields(typeInfo));

    //    var field = typeInfo.fields[fname];

    //    //These are defaults, the field is empty
    //    Assert.True(field.queryable);
    //    Assert.Null(field.onInsert);
    //    Assert.Null(field.onUpdate);
    //    Assert.Equal("otherField", field.fieldSelect);
    //    Assert.False(field.multiline);
    //    Assert.True(field.queryBuildable);
    //    Assert.Equal(0, field.expensive);
    //}


    //[Fact]
    //public void GetTypeInfoCreateDate()
    //{
    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Equal(WriteRuleType.AutoDate, typeInfo.fields["createDateField"].onInsert);
    //    Assert.Equal(WriteRuleType.Preserve, typeInfo.fields["createDateField"].onUpdate);
    //}

    //[Fact]
    //public void GetTypeInfoEditUserId()
    //{
    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Equal(WriteRuleType.DefaultValue, typeInfo.fields["editUserField"].onInsert);
    //    Assert.Equal(WriteRuleType.AutoUserId, typeInfo.fields["editUserField"].onUpdate);
    //}

    //[Fact]
    //public void GetTypeInfoFromField()
    //{
    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Equal("otherField", typeInfo.fields["remappedField"].realDbColumn); //["remappedField"]);
    //}

    //[Fact]
    //public void GetTypeInfoMultiline()
    //{
    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.True(typeInfo.fields["multilineField"].multiline); //["remappedField"]);
    //}

    //[Fact] //Ensures fields with no FromField attribute default to their actual field name
    //public void GetTypeInfoFromField_Default()
    //{
    //    var typeInfo = service.GetTypeInfo<TestView>();
    //    Assert.Equal("searchableFieldString", typeInfo.fields["searchableFieldString"].realDbColumn); //["remappedField"]);
    //}

    //[Fact]
    //public void GetTypeInfoDbObject() //Get the typeinfo for a DB object, which we still expect to fill in various fields
    //{
    //    var typeInfo = service.GetTypeInfo<Db.ContentPermission>();
    //    Assert.Equal("content_permissions", typeInfo.modelTable); //Ofc, if you change the database name, change this here too
    //}
}