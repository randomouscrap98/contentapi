using contentapi.Db;
using contentapi.Views;

namespace contentapi.Main;

public interface IDbWriter
{
    Task<T> WriteAsync<T>(T view, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task<T> DeleteAsync<T>(long id, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task ValidateUserPermissionForAction<T>(T view, UserView requester, UserAction action) where T : class, IIdView, new();
}