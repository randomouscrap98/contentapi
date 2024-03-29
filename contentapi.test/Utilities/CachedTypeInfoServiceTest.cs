using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Search;
using contentapi.data.Views;
using Xunit;
using contentapi.data;

namespace contentapi.test;

public class CachedTypeInfoServiceTest : UnitTestBase
{
    public const WriteRule DefWR = WriteRule.Preserve;

    protected ViewTypeInfoService_Cached service;

    public CachedTypeInfoServiceTest()
    {
        service = GetService<ViewTypeInfoService_Cached>();//new CachedTypeInfoService();
    }

    [ResultFor(RequestType.user)]
    [SelectFrom("cows as c")]
    [Where("something = whatever")]
    [GroupBy("magic")]
    [ExtraQueryField("id")]
    [ExtraQueryField("createDate", "c.createDate")]
    public class TestView
    {
        //An unmarked field is considered queryable, readonly, and NOT pullable by the query builder
        public long queryableFieldLong {get;set;}

        [NoQuery] //No query means you can't use this in a query... obviously
        public string unsearchableField {get;set;} = "";

        [DbField]
        public double buildableField {get;set;}

        [DbField("otherField")] //NOTE: the "as fieldname" is automatically appended... do we want that?
        public string? remappedField {get;set;}

        [DbField("e.reallyOther", "e.reallyOther", "reallyOther")] //The column field is the true database column name you want. The system SHOULD always assign this for you
        public string? joinedField {get;set;}

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
    public void GetTypeInfo_Type()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal(typeof(TestView), typeInfo.type);
    }

    [Fact] 
    public void GetTypeInfo_SelectFrom()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal("cows as c", typeInfo.selectFromSql);
    }

    [Fact] 
    public void GetTypeInfo_Where()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal("something = whatever", typeInfo.whereSql);
    }

    [Fact] 
    public void GetTypeInfo_GroupBy()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal("magic", typeInfo.groupBySql);
    }

    [Fact] 
    public void GetTypeInfo_ResultFor()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal(RequestType.user, typeInfo.requestType);
    }

    [Fact] 
    public void GetTypeInfo_ExtraQueryFields()
    {
        var typeInfo = service.GetTypeInfo<TestView>();
        Assert.Equal("id", typeInfo.extraQueryFields["id"]);
        Assert.Equal("c.createDate", typeInfo.extraQueryFields["createDate"]);
        Assert.Equal(2, typeInfo.extraQueryFields.Count);
    }

    protected IEnumerable<string> RetrievableFields(ViewTypeInfo info) => info.fields.Keys;
    //Where(x => x.Value.retrievable).Select(x => x.Key);

    protected IEnumerable<string> QueryableFields(ViewTypeInfo info) => 
        info.fields.Where(x => x.Value.queryable).Select(x => x.Key);


    [Theory]
    [InlineData("queryableFieldLong", true, DefWR, DefWR, null, null, null, false, false, 0)]
    [InlineData("unsearchableField", false, DefWR, DefWR, null, null, null, false, false, 0)]
    [InlineData("buildableField", true, DefWR, DefWR, "buildableField", "buildableField", "buildableField", false, true, 0)]
    [InlineData("remappedField", true, DefWR, DefWR, "otherField", "remappedField", "otherField", false, true, 0)]
    [InlineData("joinedField", true, DefWR, DefWR, "e.reallyOther", "e.reallyOther", "reallyOther", false, true, 0)]
    [InlineData("freeWriteField", true, WriteRule.User, WriteRule.User, null, null, null, false, false, 0)]
    [InlineData("createDateField", true, WriteRule.AutoDate, WriteRule.Preserve, null, null, null, false, false, 0)]
    [InlineData("editUserField", true, WriteRule.Preserve, WriteRule.AutoUserId, null, null, null, false, false, 0)]
    [InlineData("multilineField", true, DefWR, DefWR, null, null, null, true, false, 0)]
    [InlineData("expensiveField", true, DefWR, DefWR, null, null, null, false, false, 3)]
    public void GetTypeInfo_All(string fname, bool queryable, WriteRule onInsert, WriteRule onUpdate,
        string? fieldSelect, string? fieldWhere, string? dbColumn, bool multiline, bool queryBuildable, int expensive)
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
        Assert.Equal(fieldWhere, field.fieldWhere);
        Assert.Equal(dbColumn, field.fieldColumn);
        Assert.Equal(multiline, field.multiline);
        Assert.Equal(queryBuildable, field.queryBuildable);
        Assert.Equal(expensive, field.expensive);
    }

    // -- Tests that are specific to our types to ensure they make sense --

    [Theory] 
    [InlineData(typeof(ContentView))]
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
    //[InlineData(typeof(PageView))]
    //[InlineData(typeof(ModuleView))]
    //[InlineData(typeof(FileView))]
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
        Assert.Equal(WriteRule.Preserve, field.onInsert);
        Assert.Equal(WriteRule.Preserve, field.onUpdate);
        Assert.True(field.expensive > 0);
    }

    [Fact] 
    public void GetTypeInfo_UsersInGroups()
    {
        var typeInfo = service.GetTypeInfo<UserView>();

        var field = typeInfo.fields["usersInGroup"];

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