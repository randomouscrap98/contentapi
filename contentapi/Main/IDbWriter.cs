using contentapi.Db;
using contentapi.data.Views;
using contentapi.data;

namespace contentapi.Main;

/// <summary>
/// A DbWriter should perform all the actions necessary to allow USERS (specifically users) to modify data.
/// Thus, you should expect an IDbWriter to perform all permission and format checks necessary, it's an "all in one" package
/// </summary>
public interface IDbWriter : IDisposable
{
    Task<T> WriteAsync<T>(T view, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task<T> DeleteAsync<T>(long id, long requestUserId, string? message = null) where T : class, IIdView, new();

    Task ValidateWorkGeneral<T>(T view, T? existing, UserView requester, UserAction action) where T : class, IIdView, new();

    /// <summary>
    /// Generate a RANDOM new content hash
    /// </summary>
    /// <returns></returns>
    Task<string> GenerateContentHash(Func<string, Task> writeHash);

    /// <summary>
    /// Verify that a given hash is unique and usable
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    Task VerifyHash(string hash);

    Task<AdminLog> WriteAdminLog(AdminLog log);

    //Task<ContentView> GetDiff(long revisionA, long revisionB);
    Task<ContentView> RestoreContent(long revision, long requestUserId, string? message = null);
}