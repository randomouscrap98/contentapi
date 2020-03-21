using System.Collections.Generic;
using System.Threading.Tasks;
using contentapi.Services;

namespace contentapi.test.Overrides
{
    public class FakeEmailer : IEmailService
    {
        public List<EmailMessage> Emails = new List<EmailMessage>();

        public void SendEmail(EmailMessage message)
        {
            Emails.Add(message);
        }

        public Task SendEmailAsync(EmailMessage message)
        {
            SendEmail(message);
            return Task.CompletedTask;
        }
    }
}