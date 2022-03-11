//using System.Collections.Concurrent;
//
//namespace contentapi.Utilities;
//
//public class SignalQueue<T>
//{
//    protected AutoResetEvent signal = new AutoResetEvent(false);
//    protected ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
//    protected SemaphoreSlim queueLimit;
//
//    public SignalQueue(int queueLimit)
//    {
//        this.queueLimit = new SemaphoreSlim(queueLimit, queueLimit);
//    }
//
//    /// <summary>
//    /// Add the given item to be consumed later. Will stall if the queue is too full
//    /// </summary>
//    /// <param name="item"></param>
//    /// <returns></returns>
//    public async Task AddItemAsync(T item)
//    {
//        await queueLimit.WaitAsync();
//
//        lock(queue)
//        {
//            queue.Enqueue(item);
//            signal.Set();
//        }
//    }
//
//    /// <summary>
//    /// Consume up to the given amount. Stalls if there's nothing to consume
//    /// </summary>
//    /// <param name="maxAmount"></param>
//    /// <returns></returns>
//    public async Task<List<T>> ConsumeAsync(int maxAmount)
//    {
//        queue.
//        lock(queue)
//        {
//            var result = new List<T>();
//
//            while(result.Count < maxAmount && queue.Count != 0)
//            {
//                result.Add(queue.Dequeue());
//                queueLimit.Release();
//            }
//
//            return result;
//        }
//    }
//}