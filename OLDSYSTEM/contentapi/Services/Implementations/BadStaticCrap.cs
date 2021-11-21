using System;
using System.Threading.Tasks;

namespace contentapi.Services.Implementations
{
    public static class BadStaticCrap
    {
        public const int Retries = 5;
        public const int RetryDelayMs = 1000;

        public static async Task BasicRetry(Func<Task> awaitable)
        {
            for (var i = 1; i <= Retries; i++)
            {
                try
                {
                    await awaitable(); //services.provider.DeleteAsync(deletes.ToArray());
                    break;
                }
                catch //(Exception ex)
                {
                    //logger.LogError($"Couldn't delete old non-historic relations and values: {ex}");
                    if (i == Retries)
                        throw;
                    else
                        await Task.Delay(RetryDelayMs);
                }
            }
        }
    }
}