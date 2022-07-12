using System.Diagnostics;
using contentapi.Utilities;
using Newtonsoft.Json;

namespace contentapi.Program;

public static class SelfRunSystem
{
    /// <summary>
    /// What USERS of the process indirection should call to get their work done. For instance, the services would call this
    /// endpoint to run one of their tasks through a process
    /// </summary>
    /// <param name="runType"></param>
    /// <param name="runArgs"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<T> RunProcessAsync<T>(string runType, object? runArgs)
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Can't find self!"),
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add($"{Constants.SelfRun.RunPrefix}{runType}");

        if(runArgs != null)
            startInfo.ArgumentList.Add($"{Constants.SelfRun.ArgsPrefix}{JsonConvert.SerializeObject(runArgs)}");
        
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Can't spawn process {startInfo.FileName} with arguments {string.Join(",", startInfo.ArgumentList)}!");
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return JsonConvert.DeserializeObject<T>(output) ?? throw new InvalidOperationException($"Couldn't parse output to type {typeof(T)}: {output}");
    }

    /// <summary>
    /// Whether or not the given instance of contentapi should be run in "self run" mode, which bypasses the API and just runs
    /// some tiny work effort in a separate process
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static bool ShouldRunSelf(string[] args)
    {
        return args.Length >= 2 && args[1].StartsWith(Constants.SelfRun.RunPrefix);
    }

    /// <summary>
    /// What the PROGRAM calls if it detects that it is trying to run with a self-run system. This is ONLY called by the Program.Main!
    /// </summary>
    /// <param name="args"></param>
    public static void RunSelf(string[] args)
    {
        //Create a simple write to debug logger (might be syslog, who knows)
        var factory = LoggerFactory.Create(b => b.AddDebug());
        var runType = args[1].Substring(Constants.SelfRun.RunPrefix.Length);
        Object? result = null;

        //This is the image runtime stuff. 
        if(runType.StartsWith(Constants.SelfRun.RunImagePrefix))
        {
            var logger = factory.CreateLogger<ImageManipulator_Direct>();
            var manipulator = new ImageManipulator_Direct(logger);
        }

        //If a result was produced, output it as json. We always return json from the self run system
        if(result != null)
        {
            Console.WriteLine(JsonConvert.SerializeObject(result));
        }
    }
}