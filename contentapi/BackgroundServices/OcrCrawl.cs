
using System.Diagnostics;
using contentapi.data.Views;
using contentapi.Main;

namespace contentapi.BackgroundServices;

/// <summary>
/// The available ocr programs this crawler supports. Set to "None" to disable OCR.
/// </summary>
public enum OcrProgram 
{
    None,
    Tesseract
}

/// <summary>
/// Configuration for the OcrCrawler. If the program is set to none, the service will exit
/// </summary>
public class OcrCrawlConfig 
{
    public OcrProgram Program {get;set;} = OcrProgram.None;
    public TimeSpan Interval {get;set;} = TimeSpan.FromMinutes(1);
    public int ProcessPerInterval {get;set;} = 10;
    public string OcrValueKey {get;set;} = "ocr-crawl";
    public string PullOrder {get;set;} = "id_desc";
    public string TempLocation {get;set;} = "tempfiles";
}

public class OcrCrawl : BackgroundService
{
    protected ILogger logger;
    protected OcrCrawlConfig config;
    protected IDbServicesFactory dbfactory;
    protected IFileService fileService;

    public OcrCrawl(ILogger<OcrCrawl> logger, OcrCrawlConfig config, IDbServicesFactory dbfactory, IFileService fileService)
    {
        this.logger = logger;
        this.config = config;
        this.dbfactory = dbfactory;
        this.fileService = fileService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(config.Program == OcrProgram.None)
        {
            logger.LogDebug("No OCR set, exiting crawler");
            return Task.CompletedTask;
        }
        else if(config.ProcessPerInterval == 0)
        {
            logger.LogDebug("ProcessPerInterval set to 0, exiting OCR crawler");
            return Task.CompletedTask;
        }
        
        logger.LogDebug($"Beginning OCR crawler for program: {config.Program}, {config.ProcessPerInterval} per {config.Interval}");
        return CrawlLoop(stoppingToken);
    }

    /// <summary>
    /// Main crawl loop, simply loops until cancelled using the configured interval
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task CrawlLoop(CancellationToken token)
    {
        var lastRun = DateTime.Now;

        while(!token.IsCancellationRequested)
        {
            try
            {
                var interval = config.Interval - (DateTime.Now - lastRun);

                logger.LogDebug($"Interval to next crawl: {interval}");

                if (interval.Ticks > 0)
                    await Task.Delay(interval, token);
            }
            catch(OperationCanceledException) { /* These are expected! */ }
            catch(Exception ex)
            {
                logger.LogError($"ERROR DURING OCR: {ex}");
            }
        }
    }

    /// <summary>
    /// Do the entirety of a single loop's worth of OCRing
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task CrawlOcr(CancellationToken token)
    {
        using var searcher = dbfactory.CreateSearch();
    }

    /// <summary>
    /// Do a single OCR for the given content. Pulls the file and everything
    /// </summary>
    /// <returns></returns>
    public async Task<string> OcrContent(ContentView content, CancellationToken token)
    {
        //Data and mimetype in a tuple.
        var fileData = await fileService.GetFileAsync(content.hash, new data.GetFileModify());

        //Have to save the file (remember to remove it!)
        var fpath = Path.Join(config.TempLocation, content.hash + "." + fileData.Item2.Split("/", StringSplitOptions.RemoveEmptyEntries).Last());
        await File.WriteAllBytesAsync(fpath, fileData.Item1, token);
        
        try
        {
            //Start a process based on the configured program and get the output from it. Do it inside
            //a task so it can be cancelled and awaited and whatever
            if (config.Program == OcrProgram.Tesseract)
            {
                var startInfo = new ProcessStartInfo()
                {
                    FileName = "tesseract",
                    RedirectStandardOutput = true, // We write the ocr text directly to stdout
                    UseShellExecute = false,
                    WorkingDirectory = config.TempLocation,
                };

                startInfo.ArgumentList.Add(fpath);
                startInfo.ArgumentList.Add("stdout");

                logger.LogDebug($"Starting tesseract process: {startInfo.FileName} {string.Join(" ", startInfo.ArgumentList)}");
                var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Can't spawn process {startInfo.FileName} with arguments {string.Join(",", startInfo.ArgumentList)}!");
                var output = await process.StandardOutput.ReadToEndAsync().WaitAsync(token);
                await process.WaitForExitAsync(token);
                
                return string.Join("\n", output.Replace("\r", "").Split("\n", StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported OCR program: {config.Program}");
            }
        }
        finally
        {
            //Remove the file, it's ok if it errors out (just log the error)
            try { File.Delete(fpath); }
            catch(Exception ex) { logger.LogError($"Couldn't delete temp OCR file {fpath}: {ex}"); }
        }
    }
}