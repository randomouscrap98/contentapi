namespace contentapi.Utilities;

public class EmailLog 
{
    public EmailMessage message {get;set;} = new EmailMessage();
    public DateTime sendDate {get;set;} = DateTime.UtcNow;
}

/// <summary>
/// An email sender that doesn't actually send emails! Just logs all
/// the email attempts!
/// </summary>
public class NullEmailService : IEmailService
{
    public List<EmailLog> Log = new List<EmailLog>();

    public Task SendEmailAsync(EmailMessage message)
    {
        Log.Add(new EmailLog()
        {
            message = message
        });
        return Task.CompletedTask;
    }
}