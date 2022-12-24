using System.Net;
using System.Net.Mail;

namespace contentapi.Utilities;

public class EmailConfig
{
    public string Host { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public int Port { get; set; }

    public string Sender {get;set;} = "";
    public string SubjectFront { get; set; } = "";
}

public class EmailService : IEmailService
{
    protected ILogger<EmailService> logger;
    public EmailConfig Config;

    public EmailService(ILogger<EmailService> logger, EmailConfig config)
    {
        this.logger = logger;
        this.Config = config;
    }

    public async Task SendEmailAsync(EmailMessage message)
    {
        using (var mailMessage = new MailMessage())
        {
            message.Recipients.ForEach(x => mailMessage.To.Add(new MailAddress(x)));
            mailMessage.From = new MailAddress(Config.Sender);
            mailMessage.Subject = $"{Config.SubjectFront} - {message.Title}";
            mailMessage.Body = message.Body;

            if(message.IsHtml)
                mailMessage.IsBodyHtml = true;

            logger.LogDebug($"Sending email to {string.Join(",", message.Recipients)} using {Config.Host}:{Config.User}");

            using (var client = new SmtpClient(Config.Host))
            {
                client.Port = Config.Port;
                client.Credentials = new NetworkCredential(Config.User, Config.Password);
                client.EnableSsl = true;
                await client.SendMailAsync(mailMessage);
            }
        }
    }
}