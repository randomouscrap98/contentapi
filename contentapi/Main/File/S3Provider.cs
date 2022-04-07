using Amazon.S3;

namespace contentapi.Main;

/// <summary>
/// A very stupid class whose only existence is to mitigate the atrociously long time it takes to even construct the
/// IAmazonS3 instance to pass with dependency injection. This destroys the initial load speed of systems that are only
/// using local files (such as the debugger)
/// </summary>
public class S3Provider
{
    protected readonly object CacheLock = new Object();
    protected IAmazonS3? CachedProvider = null;
    public Func<IAmazonS3> GetRawProvider {get;set;} = () => throw new NotImplementedException("You must give a way to produce IAmazonS3!");

    public IAmazonS3 GetCachedProvider()
    {
        lock(CacheLock)
        {
            if(CachedProvider == null)
                CachedProvider = GetRawProvider();

            return CachedProvider;
        }
    }
}