using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using contentapi.Configs;

namespace contentapi.Services
{
    public class EmailMessage
    {
        public List<string> Recipients {get;set;} = new List<string>();
        public string Title {get;set;} = null;
        public string Body {get;set;} = null;

        public EmailMessage() {}

        //A simple constructor for the most common use-case
        public EmailMessage(string recipient, string title, string body)
        {
            Recipients.Add(recipient);
            Title = title;
            Body = body;
        }
    }

    public interface IEmailService
    {
        void SendEmail(EmailMessage message);
        Task SendEmailAsync(EmailMessage message);
    }

    public class EmailService : IEmailService
    {
        public EmailConfig Config = null;

        public EmailService(EmailConfig config)
        {
            this.Config = config;
        }

        public void SendEmail(EmailMessage message)
        {
            SendEmailAsync(message).Wait();
        }

        public async Task SendEmailAsync(EmailMessage message)
        {
            using(var mailMessage = new MailMessage())
            {
                message.Recipients.ForEach(x => mailMessage.To.Add(new MailAddress(x)));
                mailMessage.From = new MailAddress(Config.User);
                mailMessage.Subject = $"{Config.SubjectFront} - {message.Title}";
                mailMessage.Body = message.Body;

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