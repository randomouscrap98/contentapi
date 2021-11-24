using System.Collections.Concurrent;

namespace contentapi;

public class CheckpointTracker : ICheckpointTracker
{
    public class CheckpointData
    {
        public readonly object SignalLock = new object();
        public int Checkpoint = 0;
        public List<SemaphoreSlim> Waiters = new List<SemaphoreSlim>();
    }

    protected ConcurrentDictionary<string, CheckpointData> checkpoints = new ConcurrentDictionary<string, CheckpointData>();
    protected ILogger logger;
    protected readonly object signalLock = new Object();

    public CheckpointTracker(ILogger<CheckpointTracker> logger)
    {
        this.logger = logger;
    }

    //public int CurrentCheckpoint(string checkpointName)
    //{
    //    return checkpoints.GetOrAdd(checkpointName, s => new CheckpointData()).Checkpoint;
    //}

    protected CheckpointData GetCheckpoint(string checkpointName)
    {
        return checkpoints.GetOrAdd(checkpointName, s => new CheckpointData());
    }

    public int UpdateCheckpoint(string checkpointName)
    {
        //Need at least SOME checkpoint data first so we have a lock
        var thisCheckpoint = GetCheckpoint(checkpointName);

        //None of the code in here can throw, it's fine.
        lock(thisCheckpoint.SignalLock)
        {
            //Now LEGIT update the value. Interlocked is unnecessary because nobody can touch the value 
            //except in the lock
            Interlocked.Increment(ref   thisCheckpoint.Checkpoint);

            //Signal all waiters. Whatever, they'll all complete I guess.
            foreach(var waiter in thisCheckpoint.Waiters)
                waiter.Release();
            
            thisCheckpoint.Waiters.Clear();

            return thisCheckpoint.Checkpoint;
            //var newValue = checkpoints.AddOrUpdate(checkpointName, 
            //    (s) => new CheckpointData(), 
            //    (s,v) => { Interlocked.Increment(ref v.Checkpoint); return v; }
            //);
        }
    }

    public async Task<int> WaitForCheckpoint(string checkpointName, int lastSeen, CancellationToken cancelToken)
    {
        var thisCheckpoint = GetCheckpoint(checkpointName);
        var watchSem = new SemaphoreSlim(0,1); //Initialize a semaphore that needs to be released (0 out of 1)

        lock(thisCheckpoint.SignalLock)
        {
            //Easymode: done
            if(lastSeen < thisCheckpoint.Checkpoint)
                return thisCheckpoint.Checkpoint;

            //Oops now we wait
            thisCheckpoint.Waiters.Add(watchSem);
        }

        await watchSem.WaitAsync(cancelToken);

        //Does the value even matter? Will I EVER get a number lower than the one produced in the lock? I don't
        //really get it.. maybe it's too late.
        return Interlocked.CompareExchange(ref thisCheckpoint.Checkpoint, 0, 0);
    }
}