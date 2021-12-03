
namespace contentapi.Utilities;

/// <summary>
/// A service for easily tracking checkpointed cache data and sending updates to any volume of listeners. 
/// Useful for a live data service with reconnects, as tracking the last checkpoint is easy, and waiting for
/// checkpoints before the end of the cache returns instantly
/// </summary>
public interface ICacheCheckpointTracker
{
    int UpdateCheckpoint(string checkpointName, object newValue);

    Task<CacheCheckpointResult> WaitForCheckpoint(string checkpointName, int lastSeen, CancellationToken cancelToken);
}