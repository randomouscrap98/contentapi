using System.Collections.Generic;
using System.Threading.Tasks;

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
        Task SendEmailAsync(EmailMessage message);
    }

}