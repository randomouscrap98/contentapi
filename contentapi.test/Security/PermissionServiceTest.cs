using System;
using contentapi.data;
using contentapi.Db;
using Xunit;

namespace contentapi.test;

public class PermissionServiceTest : UnitTestBase
{
    protected PermissionService service;

    public PermissionServiceTest()
    {
        service = GetService<PermissionService>();
    }

    [Fact]
    public void ActionToStringTransparent()
    {
        foreach(var action in Enum.GetValues<UserAction>())
        {
            var str = service.ActionToString(action);
            var transact = service.StringToAction(str);
            Assert.Equal(action, transact);
        }
    }
}