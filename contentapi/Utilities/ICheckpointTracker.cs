namespace contentapi.Utilities;

public interface ICheckpointTracker
{
    int UpdateCheckpoint(string checkpointName);
    Task<int> WaitForCheckpoint(string checkpointName, int lastSeen, CancellationToken cancelToken);
    //int CurrentCheckpoint(string checkpointName);
}