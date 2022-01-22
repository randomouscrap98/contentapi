namespace contentapi.Utilities;

public class EmailMessage
{
    public List<string> Recipients { get; set; } = new List<string>();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    public EmailMessage() { }

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
