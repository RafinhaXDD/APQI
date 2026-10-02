namespace BookExchange.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string TextBody);

/// <summary>Sends one email. Implementations must never log message bodies (they contain links, R-20).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
