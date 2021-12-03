using System.Collections.Concurrent;

namespace contentapi.Utilities;

public class CacheCheckpointTrackerConfig
{
    public TimeSpan CacheAge {get;set;} = TimeSpan.FromHours(24);
    public int CacheCleanFrequency {get;set;} = 100;
}

public class CacheCheckpointTracker : ICacheCheckpointTracker
{
    public class CacheData 
    {
        public object data = 0;
        public DateTime date;
    }

    public class CheckpointData
    {
        public readonly object SignalLock = new object();
        public int Checkpoint = 0;
        public Dictionary<int, CacheData> Cache = new Dictionary<int, CacheData>();
        public List<SemaphoreSlim> Waiters = new List<SemaphoreSlim>();
    }

    protected ConcurrentDictionary<string, CheckpointData> checkpoints = new ConcurrentDictionary<string, CheckpointData>();
    protected ILogger logger;
    protected CacheCheckpointTrackerConfig config;

    public CacheCheckpointTracker(ILogger<CacheCheckpointTracker> logger, CacheCheckpointTrackerConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    protected CheckpointData GetCheckpoint(string checkpointName)
    {
        return checkpoints.GetOrAdd(checkpointName, s => new CheckpointData());
    }

    public int UpdateCheckpoint(string checkpointName, object newValue)
    {
        //Need at least SOME checkpoint data first so we have a lock
        var thisCheckpoint = GetCheckpoint(checkpointName);

        //None of the code in here can throw, it's fine.
        lock(thisCheckpoint.SignalLock)
        {
            //Now LEGIT update the value. Interlocked is unnecessary because nobody can touch the value 
            //except in the lock
            int newKey = Interlocked.Increment(ref thisCheckpoint.Checkpoint);

            if((newKey % config.CacheCleanFrequency) == 0)
            {
                var removeKeys = thisCheckpoint.Cache.Where(x => (DateTime.Now - x.Value.date) > config.CacheAge).Select(x => x.Key);
                foreach(var key in removeKeys)
                    thisCheckpoint.Cache.Remove(key);
            }

            thisCheckpoint.Cache.Add(newKey, new CacheData { data = newValue, date = DateTime.Now });

            //Signal all waiters. Whatever, they'll all complete I guess.
            foreach(var waiter in thisCheckpoint.Waiters)
                waiter.Release();
            
            thisCheckpoint.Waiters.Clear();

            return thisCheckpoint.Checkpoint;
        }
    }

    protected List<object> CacheAfter(CheckpointData checkpoint, int lastSeen)
    {
        return checkpoint.Cache.Where(x => x.Key > lastSeen).Select(x => x.Value.data).ToList();
    }

    public async Task<CacheCheckpointResult> WaitForCheckpoint(string checkpointName, int lastSeen, CancellationToken cancelToken)
    {
        var thisCheckpoint = GetCheckpoint(checkpointName);
        var watchSem = new SemaphoreSlim(0,1); //Initialize a semaphore that needs to be released (0 out of 1)

        lock(thisCheckpoint.SignalLock)
        {
            //The request is TOO OLD, it's beyond the end of the cache!
            if(lastSeen > 0 && lastSeen < thisCheckpoint.Cache.Keys.Min())
                throw new ExpiredCheckpointException($"Checkpoint {lastSeen} is too old! You will be missing cached data!");

            //Easymode: done. Doesn't matter if the list is empty, we honor exactly what the checkpoint says. The caller has to decide
            //what to do when a list is empty.
            if(lastSeen < thisCheckpoint.Checkpoint)
                return new CacheCheckpointResult { LastId = thisCheckpoint.Checkpoint, Data = CacheAfter(thisCheckpoint, lastSeen) };

            //Oops now we wait, there was nothing.
            thisCheckpoint.Waiters.Add(watchSem);
        }

        await watchSem.WaitAsync(cancelToken);

        //We know that if we were released, SOMETHING must've been in here!
        return new CacheCheckpointResult { LastId = Interlocked.CompareExchange (ref thisCheckpoint.Checkpoint, 0, 0), Data = CacheAfter(thisCheckpoint, lastSeen) };
    }
}