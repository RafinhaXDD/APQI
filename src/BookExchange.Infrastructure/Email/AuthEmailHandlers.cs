using System.Text.Json;
using BookExchange.Application.Abstractions;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Identity;
using BookExchange.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookExchange.Infrastructure.Email;

public static class AuthEmails
{
    public const string ConfirmEmail = "email.confirm-email";
    public const string PasswordReset = "email.password-reset";
    public const string AccountExists = "email.account-exists";
}

/// <summary>Only the user id is stored; one-time tokens are generated when the email is sent.</summary>
public sealed record AuthEmailPayload(Guid UserId);

/// <summary>
/// Base for auth emails sent from the outbox. Delivery is at least once: a retry after an SMTP failure
/// may send a second email, which is harmless (the newest link works; older ones stay valid until expiry).
/// </summary>
internal abstract class AuthEmailHandler(
    UserManager<AppUser> users,
    AppDbContext db,
    IEmailSender sender,
    IOptions<EmailOptions> options) : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract string MessageType { get; }

    protected UserManager<AppUser> Users => users;

    protected Uri AppBaseUrl => options.Value.AppBaseUrl;

    public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<AuthEmailPayload>(message.Payload, JsonOptions)
            ?? throw new InvalidOperationException("Empty auth email payload.");
        var user = await users.FindByIdAsync(payload.UserId.ToString());
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == payload.UserId, cancellationToken);
        if (user?.Email is null || profile is null)
        {
            return; // Account no longer exists: nothing to send.
        }

        var content = await BuildAsync(user, profile.DisplayName, profile.PreferredLanguage);
        if (content is not null)
        {
            await sender.SendAsync(new EmailMessage(user.Email, content.Subject, content.Body), cancellationToken);
        }
    }

    /// <returns>null when the email is no longer relevant (e.g. already confirmed).</returns>
    protected abstract Task<EmailContent?> BuildAsync(AppUser user, string name, string language);

    protected Uri Link(string path, AppUser user, string? token = null)
    {
        var query = token is null ? string.Empty : $"?userId={user.Id}&token={AuthService.EncodeToken(token)}";
        return new Uri(AppBaseUrl, path + query);
    }
}

internal sealed class ConfirmEmailHandler(UserManager<AppUser> users, AppDbContext db, IEmailSender sender, IOptions<EmailOptions> options)
    : AuthEmailHandler(users, db, sender, options)
{
    public override string MessageType => AuthEmails.ConfirmEmail;

    protected override async Task<EmailContent?> BuildAsync(AppUser user, string name, string language)
    {
        if (user.EmailConfirmed)
        {
            return null;
        }

        var token = await Users.GenerateEmailConfirmationTokenAsync(user);
        return EmailTemplates.ConfirmEmail(language, name, Link("/confirm-email", user, token));
    }
}

internal sealed class PasswordResetHandler(UserManager<AppUser> users, AppDbContext db, IEmailSender sender, IOptions<EmailOptions> options)
    : AuthEmailHandler(users, db, sender, options)
{
    public override string MessageType => AuthEmails.PasswordReset;

    protected override async Task<EmailContent?> BuildAsync(AppUser user, string name, string language)
    {
        var token = await Users.GeneratePasswordResetTokenAsync(user);
        return EmailTemplates.PasswordReset(language, name, Link("/reset-password", user, token));
    }
}

internal sealed class AccountExistsHandler(UserManager<AppUser> users, AppDbContext db, IEmailSender sender, IOptions<EmailOptions> options)
    : AuthEmailHandler(users, db, sender, options)
{
    public override string MessageType => AuthEmails.AccountExists;

    protected override Task<EmailContent?> BuildAsync(AppUser user, string name, string language) =>
        Task.FromResult<EmailContent?>(EmailTemplates.AccountExists(
            language, name, new Uri(AppBaseUrl, "/login"), new Uri(AppBaseUrl, "/forgot-password")));
}
