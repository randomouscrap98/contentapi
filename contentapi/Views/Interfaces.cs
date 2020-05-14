using System;
using System.Collections.Generic;

namespace contentapi.Views
{
    /// <summary>
    /// The MOST basic view: just has an id
    /// </summary>
    public interface IIdView
    {
        long id {get;set;}
    }

    /// <summary>
    /// A basic view from the entity system: they all have create dates.
    /// </summary>
    public interface IBaseView : IIdView
    {
        DateTime createDate {get;set;}
    }

    /// <summary>
    /// A basic view from the history system: they all have full edit/create info
    /// </summary>
    public interface IEditView : IBaseView
    {
        DateTime editDate {get;set;}
        long createUserId {get;set;}
        long editUserId {get;set;}
    }

    public interface IUserViewBasic : IBaseView
    {
        long avatar {get;set;}
        string username {get;set;}
    }

    /// <summary>
    /// Views that have permissions MUST have parents, also show own permissions
    /// </summary>
    public interface IPermissionView : IBaseView
    {
        long parentId {get;set;}
        Dictionary<string, string> permissions {get;set;}
        string myPerms {get;set;}
    }

    /// <summary>
    /// Views that have an array of associated values.
    /// </summary>
    public interface IValueView : IBaseView
    {
        Dictionary<string,string> values {get;set;}
    }
}