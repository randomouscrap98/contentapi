using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Search;
using contentapi.Views;
using Xunit;

namespace contentapi.test;

public class CachedTypeInfoServiceTest : UnitTestBase
{
    protected ViewTypeInfoService_Cached service;

    public CachedTypeInfoServiceTest()
    {
        service = GetService<ViewTypeInfoService_Cached>();//new CachedTypeInfoService();
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

    protected IEnumerable<string> RetrievableFields(ViewTypeInfo info) => info.fields.Keys;
    //Where(x => x.Value.retrievable).Select(x => x.Key);

    protected IEnumerable<string> QueryableFields(ViewTypeInfo info) => 
        info.fields.Where(x => x.Value.queryable).Select(x => x.Key);


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

        var field = typeInfo.fields[fname];

        //These are defaults, the field is empty
        Assert.Equal(queryable, field.queryable);
        Assert.Equal(onInsert, field.onInsert);
        Assert.Equal(onUpdate, field.onUpdate);
        Assert.Equal(fieldSelect, field.fieldSelect);
        Assert.Equal(multiline, field.multiline);
        Assert.Equal(queryBuildable, field.queryBuildable);
        Assert.Equal(expensive, field.expensive);
    }

    // -- Tests that are specific to our types to ensure they make sense --

    [Theory] 
    [InlineData(typeof(ContentView))]
    [InlineData(typeof(PageView))]
    [InlineData(typeof(ModuleView))]
    [InlineData(typeof(FileView))]
    public void GetTypeInfo_ContentPermission(Type t)
    {
        var typeInfo = service.GetTypeInfo(t);

        var field = typeInfo.fields["permissions"];

        //Now ensure permissions make sense. We won't do this for ALL fields, but we've had
        //problems with permissions before, and might as well check the system. Permissions are 
        //one of the most important things to get right anyway.
        Assert.True(string.IsNullOrEmpty(field.fieldSelect), "Permissions had a backing field select set when it shouldn't!");
        Assert.False(field.queryBuildable);
        Assert.False(field.queryable);
        Assert.Equal(WriteRule.User, field.onInsert);
        Assert.Equal(WriteRule.User, field.onUpdate);
        Assert.True(field.expensive > 0);
    }

    [Theory] 
    [InlineData(typeof(ContentView))]
    [InlineData(typeof(PageView))]
    [InlineData(typeof(ModuleView))]
    [InlineData(typeof(FileView))]
    public void GetTypeInfo_ContentGeneral(Type t)
    {
        var typeInfo = service.GetTypeInfo(t);

        //This might be a fragile test but whatever, better safe than sorry
        Assert.Equal("content AS main", typeInfo.selectFromSql);
        Assert.NotNull(typeInfo.writeAsInfo);
        Assert.Equal(typeof(Db.Content), typeInfo.writeAsInfo?.modelType);
    }

    [Fact] 
    public void GetTypeInfo_UserGroups()
    {
        var typeInfo = service.GetTypeInfo<UserView>();

        var field = typeInfo.fields["groups"];

        //Now ensure permissions make sense. We won't do this for ALL fields, but we've had
        //problems with permissions before, and might as well check the system. Permissions are 
        //one of the most important things to get right anyway.
        Assert.True(string.IsNullOrEmpty(field.fieldSelect), "Groups had a backing field select set when it shouldn't!");
        Assert.False(field.queryBuildable);
        Assert.False(field.queryable);
        Assert.Equal(WriteRule.User, field.onInsert);
        Assert.Equal(WriteRule.User, field.onUpdate);
        Assert.True(field.expensive > 0);
    }
}