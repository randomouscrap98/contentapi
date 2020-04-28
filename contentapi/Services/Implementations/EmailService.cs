using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class EmailConfig
    {
        public string Host {get;set;}
        public string User {get;set;}
        public string Password {get;set;}
        public int Port {get;set;}

        public string SubjectFront {get;set;}
    }

    public class EmailService : IEmailService
    {
        protected ILogger<EmailService> logger;
        public EmailConfig Config = null;

        public EmailService(ILogger<EmailService> logger, EmailConfig config)
        {
            this.logger = logger;
            this.Config = config;
        }

        public async Task SendEmailAsync(EmailMessage message)
        {
            using(var mailMessage = new MailMessage())
            {
                message.Recipients.ForEach(x => mailMessage.To.Add(new MailAddress(x)));
                mailMessage.From = new MailAddress(Config.User);
                mailMessage.Subject = $"{Config.SubjectFront} - {message.Title}";
                mailMessage.Body = message.Body;

                logger.LogDebug($"Sending email to {string.Join(",", message.Recipients)} using {Config.Host}:{Config.User}");

                using(var client = new SmtpClient(Config.Host))
                {
                    client.Port = Config.Port;
                    client.Credentials = new NetworkCredential(Config.User, Config.Password);
                    client.EnableSsl = true;
                    await client.SendMailAsync(mailMessage);
                }
            }
        }
    }
}