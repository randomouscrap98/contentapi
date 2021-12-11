using contentapi.Db;
using contentapi.Views;

namespace contentapi.Main;

/// <summary>
/// A DbWriter should perform all the actions necessary to allow USERS (specifically users) to modify data.
/// Thus, you should expect an IDbWriter to perform all permission and format checks necessary, it's an "all in one" package
/// </summary>
public interface IDbWriter
{
    Task<T> WriteAsync<T>(T view, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task<T> DeleteAsync<T>(long id, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task ValidateUserPermissionForAction<T>(T view, T? existing, UserView requester, UserAction action) where T : class, IIdView, new();
}