using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

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