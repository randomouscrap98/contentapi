using System.Text;

namespace contentapi.Utilities;

public class EmailLog 
{
    public EmailMessage message {get;set;} = new EmailMessage();
    public DateTime sendDate {get;set;} = DateTime.UtcNow;
}

public class FileEmailServiceConfig
{
    public string Folder {get;set;} = "";
}

/// <summary>
/// An email sender that doesn't actually send emails! Just logs all emails
/// to the filesystem!
/// </summary>
public class FileEmailService : IEmailService
{
    //public List<EmailLog> Log = new List<EmailLog>();
    protected FileEmailServiceConfig config;
    protected ILogger logger;

    public FileEmailService(ILogger<FileEmailService> logger, FileEmailServiceConfig config)
    {
        this.logger = logger;
        this.config = config;
    }

    public async Task SendEmailAsync(EmailMessage message)
    {
        logger.LogDebug($"Sending email '{message.Title}' to {string.Join(",", message.Recipients)}");

        foreach(var recipient in message.Recipients)
        {
            var folder = Path.Combine(config.Folder, StaticUtils.SafeFolderName(recipient));
            Directory.CreateDirectory(folder);
            var filename = StaticUtils.SafeFileName(DateTime.Now.ToString("yyyyMMddTHHmmss") + "_" + message.Title + ".txt");
            var fullPath = Path.Combine(folder, filename);
            await File.WriteAllTextAsync(fullPath, message.Body);
        }
    }
}