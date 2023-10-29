using contentapi.Live;
using contentapi.Main;

namespace contentapi.BackgroundServices;

public class GenericTaskService : BackgroundService
{
    protected ILogger logger;

    private static List<Task> Tasks = new();
    private static SemaphoreSlim TaskAdded = new SemaphoreSlim(0, 1);

    public GenericTaskService(ILogger<GenericTaskService> logger)
    {
        this.logger = logger;
    }

    public static void AddTask(Task task)
    {
        lock(Tasks) {
            Tasks.Add(task);
        }

        try { TaskAdded.Release(); }
        catch { /* Do nothing, it's ok */ }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting generic task service");

        while(!stoppingToken.IsCancellationRequested)
        {
            List<Task> tasks;

            lock(Tasks)
            {
                tasks = new List<Task>(Tasks);
            }

            //This is both a cancellation (assuming the semaphore will yield on cancel) AND a wait for task added
            tasks.Add(TaskAdded.WaitAsync(stoppingToken));

            var completedTask = await Task.WhenAny(tasks);

            if(completedTask.IsFaulted)
                logger.LogWarning($"Exception from generic task: {completedTask.Exception}");

            lock(Tasks)
            {
                Tasks.Remove(completedTask);
            }
        }
    }
}