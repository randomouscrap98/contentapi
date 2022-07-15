using System.Diagnostics;
using Newtonsoft.Json;

namespace contentapi.SelfRun;

public interface IInfileArgument
{
    string inFile {get;set;}
}

/// <summary>
/// A wacky class that acts as an overseer for running certain calls through a spawned process rather
/// than calling them directly. The actual reimplementations of the interfaces for which this works 
/// are located in the folder, adjacent to this class
/// </summary>
public class SelfRunSystem
{
    public const string RunPrefix = "run=";
    public const string ArgsPrefix = "args=";
    public const string RunImagePrefix = "image.";
    public const string RunImageResize = RunImagePrefix + "size";
    public const string RunImageThumbnail = RunImagePrefix + "thumbnail";

    public static string TempLocation {get;set;} = "tempfiles";

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

        startInfo.ArgumentList.Add($"{RunPrefix}{runType}");

        if(runArgs != null)
            startInfo.ArgumentList.Add($"{ArgsPrefix}{JsonConvert.SerializeObject(runArgs)}");
        
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Can't spawn process {startInfo.FileName} with arguments {string.Join(",", startInfo.ArgumentList)}!");
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if(process.ExitCode != 0)
        {
            throw new InvalidOperationException(output);
        }
        else
        {
            return JsonConvert.DeserializeObject<T>(output) ?? throw new InvalidOperationException($"Couldn't parse output to type {typeof(T)}: {output}");
        }

    }

    /// <summary>
    /// A wrapper for RunProcess that lets you pass a file as part of the argument list (as a stream)
    /// </summary>
    /// <param name="fileData"></param>
    /// <param name="runType"></param>
    /// <param name="args"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<T> RunProcessWithFileAsync<T>(Stream fileData, string runType, IInfileArgument args)
    {
        Directory.CreateDirectory(TempLocation);
        var tempFile = Path.Combine(TempLocation, Guid.NewGuid().ToString().Replace("-", ""));

        using(var file = File.Create(tempFile))
        {
            fileData.Seek(0, SeekOrigin.Begin);
            await fileData.CopyToAsync(file);
        }

        args.inFile = tempFile;

        try
        {
            return await SelfRunSystem.RunProcessAsync<T>(runType, args);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    public static T ParseArgument<T>(string argument)
    {
        return JsonConvert.DeserializeObject<T>(argument) ?? throw new InvalidOperationException($"Couldn't parse arg to type {typeof(T)}");
    }


    /// <summary>
    /// Whether or not the given instance of contentapi should be run in "self run" mode, which bypasses the API and just runs
    /// some tiny work effort in a separate process
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static bool ShouldRunSelf(string[] args)
    {
        return args.Length >= 2 && args[1].StartsWith(RunPrefix);
    }

    /// <summary>
    /// What the PROGRAM calls if it detects that it is trying to run with a self-run system. This is ONLY called by the Program.Main!
    /// </summary>
    /// <param name="args"></param>
    public static async void RunSelf(string[] args)
    {
        try
        {
            //Create a simple write to debug logger (might be syslog, who knows)
            var factory = LoggerFactory.Create(b => b.AddDebug());
            var runType = args[1].Substring(RunPrefix.Length);
            Object? result = null;

            //This is the image runtime stuff. 
            if (runType.StartsWith(RunImagePrefix))
            {
                if(args.Length < 3)
                    throw new InvalidOperationException("Not enough arguments!");

                result = await ImageManipulator_Process.SelfRunCall(runType, args[2], factory);
            }

            //If a result was produced, output it as json. We always return json from the self run system
            if (result != null)
            {
                Console.WriteLine(JsonConvert.SerializeObject(result));
            }

            Environment.Exit(0);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"EXCEPTION: {ex}");
            Environment.Exit(99);
        }
    }

    
}