using contentapi.BackgroundServices;

namespace contentapi;

/// <summary>
/// A service to dump tasks into which should complete in the background, separate from web requests or anything. Will be
/// cancelled on shutdown
/// </summary>
public interface IQueueBackgroundTask
{
    public void AddTask(Task task);
}


public class QueueBackgroundTask : IQueueBackgroundTask
{
    public void AddTask(Task task) => GenericTaskService.AddTask(task);
}