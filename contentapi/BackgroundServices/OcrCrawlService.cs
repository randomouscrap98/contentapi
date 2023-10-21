
using System.Diagnostics;
using System.Net.Mime;
using System.Text.RegularExpressions;
using contentapi.data;
using contentapi.data.Views;
using contentapi.Main;
using contentapi.Search;

namespace contentapi.BackgroundServices;

/// <summary>
/// The available ocr programs this crawler supports. Set to "None" to disable OCR.
/// </summary>
public enum OcrProgram 
{
    none,
    Tesseract
}

/// <summary>
/// Configuration for the OcrCrawler. If the program is set to none, the service will exit
/// </summary>
public class OcrCrawlConfig 
{
    public OcrProgram Program {get;set;} = OcrProgram.none;
    public TimeSpan Interval {get;set;} = TimeSpan.FromMinutes(1);
    public int ProcessPerInterval {get;set;} = 10;
    public string OcrValueKey {get;set;} = "ocr-crawl";
    public string OcrFailKey {get;set;} = "ocr-fail";
    public string PullOrder {get;set;} = "id_desc";
    public string TempLocation {get;set;} = "tempfiles";
}

public class OcrCrawlService : BackgroundService
{
    protected ILogger logger;
    protected OcrCrawlConfig config;
    protected IDbServicesFactory dbfactory;
    protected IFileService fileService;

    public OcrCrawlService(ILogger<OcrCrawlService> logger, OcrCrawlConfig config, IDbServicesFactory dbfactory, IFileService fileService)
    {
        this.logger = logger;
        this.config = config;
        this.dbfactory = dbfactory;
        this.fileService = fileService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if(config.Program == OcrProgram.none)
        {
            logger.LogInformation("No OCR set, exiting crawler");
            return Task.CompletedTask;
        }
        else if(config.ProcessPerInterval == 0)
        {
            logger.LogInformation("ProcessPerInterval set to 0, exiting OCR crawler");
            return Task.CompletedTask;
        }
        
        logger.LogInformation($"Beginning OCR crawler for program: {config.Program}, {config.ProcessPerInterval} per {config.Interval}");
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
                
                await OcrBatch(token);
            }
            catch(OperationCanceledException) { /* These are expected! */ }
            catch(Exception ex)
            {
                logger.LogError($"ERROR DURING OCR: {ex}");
            }
            finally
            {
                lastRun = DateTime.Now;
            }
        }
    }

    /// <summary>
    /// Do the entirety of a single loop's worth of OCRing
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task OcrBatch(CancellationToken token)
    {
        using var searcher = dbfactory.CreateSearch();
        using var writer = dbfactory.CreateWriter();

        //Grab content
        var fileViews = await searcher.SearchSingleTypeUnrestricted<ContentView>(new SearchRequest() {
            type = nameof(RequestType.content),
            limit = config.ProcessPerInterval,
            order = config.PullOrder,
            fields = "*", //Since we expect to write this back
            query = "contentType = @filetype and !valuekeynotin(@ocrkey)"
        }, new Dictionary<string, object> { 
            { "filetype", InternalContentType.file },
            { "ocrkey", new[] { config.OcrValueKey, config.OcrFailKey } },
        });

        if(fileViews.Count > 0)
            logger.LogDebug($"Begin OCR crawl on {fileViews.Count} files: {string.Join(",", fileViews.Select(x => x.hash))}");
        else
            logger.LogDebug("No files to OCR crawl, skipping");

        foreach(var file in fileViews)
        {
            string ocr = "";
            try
            {
                ocr = await OcrContent(file, token);
                file.values[config.OcrValueKey] = ocr;
            }
            catch(Exception ex)
            {
                logger.LogError($"Couldn't process ocr on file {file.hash}: {ex}");
                file.values[config.OcrFailKey] = ex.Message;
            }
            await writer.WriteAsync(file, file.createUserId, $"OCR service: {Constants.ToCommonDateString(DateTime.UtcNow)}");
            logger.LogInformation($"Wrote OCR for file {file.hash}({file.id}): {ocr.Length} chars");
        }
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
        System.IO.Directory.CreateDirectory(config.TempLocation);
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

                startInfo.ArgumentList.Add(Path.GetFileName(fpath));
                startInfo.ArgumentList.Add("stdout");

                logger.LogDebug($"Starting tesseract process: {startInfo.FileName} {string.Join(" ", startInfo.ArgumentList)}");
                var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Can't spawn process {startInfo.FileName} with arguments {string.Join(",", startInfo.ArgumentList)}!");
                var output = await process.StandardOutput.ReadToEndAsync().WaitAsync(token);
                await process.WaitForExitAsync(token);
                
                return string.Join("\n", Regex.Replace(output.Replace("\r", ""), @"\s+", " ").Split("\n", StringSplitOptions.RemoveEmptyEntries)).Trim();
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