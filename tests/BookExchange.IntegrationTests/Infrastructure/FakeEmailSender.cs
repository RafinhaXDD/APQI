using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BookExchange.Application.Abstractions;

namespace BookExchange.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> To(string email) =>
        Sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>The userId and token from the newest link to <paramref name="path"/> sent to <paramref name="email"/>.</summary>
    public (Guid UserId, string Token) LatestLink(string email, string path)
    {
        var message = To(email).LastOrDefault(m => m.TextBody.Contains(path, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No email with {path} was sent to {email}.");
        var match = LinkPattern().Match(message.TextBody);
        return match.Success
            ? (Guid.Parse(match.Groups["user"].Value), match.Groups["token"].Value)
            : throw new InvalidOperationException("No link with userId and token in the email.");
    }

    [GeneratedRegex(@"userId=(?<user>[0-9a-f-]{36})&token=(?<token>[A-Za-z0-9_-]+)")]
    private static partial Regex LinkPattern();
}
