using System.Buffers.Text;
using System.Text;
using BookExchange.Application.Auth;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Domain.Credits;
using BookExchange.Domain.Users;
using BookExchange.Infrastructure.Email;
using BookExchange.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookExchange.Infrastructure.Identity;

public enum AuthFailure
{
    InvalidCredentials,
    EmailNotConfirmed,
    SessionExpired,
    InvalidLink,
    WrongPassword,
    PasswordRejected,
}

public sealed record SessionTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public sealed record AuthResult<T>(T? Value, AuthFailure? Failure, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Failure is null;
}

public static class AuthResult
{
    public static AuthResult<T> Ok<T>(T value) => new(value, null, []);

    public static AuthResult<T> Fail<T>(AuthFailure failure, IReadOnlyList<string>? errors = null) => new(default, failure, errors ?? []);
}

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> users,
    IOutbox outbox,
    AccessTokenIssuer accessTokens,
    PasswordTimingGuard timingGuard,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock)
{
    /// <summary>
    /// Always "accepted" for the caller (R-19). New email: account + profile + credit account created and a
    /// confirmation email queued. Existing email: a "someone tried to register" email is queued instead.
    /// </summary>
    public async Task<AuthResult<bool>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            timingGuard.SimulateHash(request.Password);
            outbox.Enqueue(AuthEmails.AccountExists, new AuthEmailPayload(existing.Id));
            await db.SaveChangesAsync(cancellationToken);
            return AuthResult.Ok<bool>(true);
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new AppUser { UserName = email, Email = email, CreatedAt = now };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // Identity options mirror the request validator, so this means a concurrent registration of the
            // same email; answer exactly as for an existing email.
            if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return AuthResult.Ok<bool>(true);
            }

            return AuthResult.Fail<bool>(AuthFailure.PasswordRejected, created.Errors.Select(e => e.Description).ToList());
        }

        var language = SupportedLanguages.IsSupported(request.PreferredLanguage) ? request.PreferredLanguage! : SupportedLanguages.Default;
        db.UserProfiles.Add(UserProfile.Create(user.Id, request.DisplayName, language, now));
        db.CreditAccounts.Add(CreditAccount.Open(user.Id));
        outbox.Enqueue(AuthEmails.ConfirmEmail, new AuthEmailPayload(user.Id));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AuthResult.Ok<bool>(true);
    }

    public async Task<AuthResult<SessionTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || await users.IsLockedOutAsync(user))
        {
            timingGuard.SimulateVerify(request.Password);
            return AuthResult.Fail<SessionTokens>(AuthFailure.InvalidCredentials);
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return AuthResult.Fail<SessionTokens>(AuthFailure.InvalidCredentials);
        }

        // Only reachable with the correct password, so it reveals nothing to someone guessing.
        if (!user.EmailConfirmed)
        {
            return AuthResult.Fail<SessionTokens>(AuthFailure.EmailNotConfirmed);
        }

        await users.ResetAccessFailedCountAsync(user);
        var session = StartSession(user, familyId: Guid.CreateVersion7());
        await db.SaveChangesAsync(cancellationToken);
        return AuthResult.Ok<SessionTokens>(session);
    }

    /// <summary>Rotates the refresh token (R-18). Reusing a rotated token revokes the whole family.</summary>
    public async Task<AuthResult<SessionTokens>> RefreshAsync(string? rawToken, CancellationToken cancellationToken)
    {
        var current = await FindTokenAsync(rawToken, cancellationToken);
        if (current is null)
        {
            return AuthResult.Fail<SessionTokens>(AuthFailure.SessionExpired);
        }

        var now = clock.GetUtcNow();
        if (current.RevokedReason == RefreshTokenRevocation.Rotated)
        {
            await RevokeFamilyAsync(current.FamilyId, RefreshTokenRevocation.ReuseDetected, cancellationToken);
            return AuthResult.Fail<SessionTokens>(AuthFailure.SessionExpired);
        }

        var user = current.IsActive(now) ? await users.FindByIdAsync(current.UserId.ToString()) : null;
        if (user is null || await users.IsLockedOutAsync(user))
        {
            return AuthResult.Fail<SessionTokens>(AuthFailure.SessionExpired);
        }

        var session = StartSession(user, current.FamilyId, out var next);
        current.Revoke(now, RefreshTokenRevocation.Rotated, next.Id);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request rotated this token a moment ago (e.g. two tabs). Not a theft signal.
            return AuthResult.Fail<SessionTokens>(AuthFailure.SessionExpired);
        }

        return AuthResult.Ok<SessionTokens>(session);
    }

    public async Task LogoutAsync(string? rawToken, CancellationToken cancellationToken)
    {
        var current = await FindTokenAsync(rawToken, cancellationToken);
        if (current is not null)
        {
            await RevokeFamilyAsync(current.FamilyId, RefreshTokenRevocation.Logout, cancellationToken);
        }
    }

    /// <summary>
    /// Confirms the email. No ficha here any more (CLAUDE.md, Phase 5b): the starter ficha comes with the
    /// first published book that has a photo.
    /// </summary>
    public async Task<AuthResult<bool>> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);
        if (user is null || token is null)
        {
            return AuthResult.Fail<bool>(AuthFailure.InvalidLink);
        }

        var confirmed = await users.ConfirmEmailAsync(user, token);
        return confirmed.Succeeded ? AuthResult.Ok<bool>(true) : AuthResult.Fail<bool>(AuthFailure.InvalidLink);
    }

    /// <summary>Queues a new confirmation email for unconfirmed accounts; silent otherwise (R-19).</summary>
    public async Task ResendConfirmationAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false })
        {
            outbox.Enqueue(AuthEmails.ConfirmEmail, new AuthEmailPayload(user.Id));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Queues a reset email for confirmed accounts; silent otherwise (R-19).</summary>
    public async Task ForgotPasswordAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: true })
        {
            outbox.Enqueue(AuthEmails.PasswordReset, new AuthEmailPayload(user.Id));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Sets a new password from an emailed link and signs out every session.</summary>
    public async Task<AuthResult<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(request.UserId.ToString());
        var token = DecodeToken(request.Token);
        if (user is null || token is null)
        {
            return AuthResult.Fail<bool>(AuthFailure.InvalidLink);
        }

        var reset = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (!reset.Succeeded)
        {
            return reset.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? AuthResult.Fail<bool>(AuthFailure.InvalidLink)
                : AuthResult.Fail<bool>(AuthFailure.PasswordRejected, reset.Errors.Select(e => e.Description).ToList());
        }

        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await RevokeUserSessionsAsync(user.Id, keepFamilyId: null, cancellationToken);
        return AuthResult.Ok<bool>(true);
    }

    /// <summary>Changes the password and signs out every other session; the current one stays.</summary>
    public async Task<AuthResult<bool>> ChangePasswordAsync(Guid userId, Guid? currentSessionId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthResult.Fail<bool>(AuthFailure.SessionExpired);
        }

        var changed = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            return changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? AuthResult.Fail<bool>(AuthFailure.WrongPassword)
                : AuthResult.Fail<bool>(AuthFailure.PasswordRejected, changed.Errors.Select(e => e.Description).ToList());
        }

        await RevokeUserSessionsAsync(user.Id, currentSessionId, cancellationToken);
        return AuthResult.Ok<bool>(true);
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return user is null || profile is null
            ? null
            : new MeResponse(user.Id, user.Email!, user.EmailConfirmed, profile.DisplayName, profile.PreferredLanguage);
    }

    private SessionTokens StartSession(AppUser user, Guid familyId) => StartSession(user, familyId, out _);

    private SessionTokens StartSession(AppUser user, Guid familyId, out RefreshToken refreshToken)
    {
        var now = clock.GetUtcNow();
        var (token, raw) = RefreshToken.Issue(user.Id, familyId, now, jwtOptions.Value.RefreshTokenLifetime);
        db.RefreshTokens.Add(token);
        refreshToken = token;

        var (access, accessExpiresAt) = accessTokens.Issue(user, familyId);
        return new SessionTokens(access, accessExpiresAt, raw, token.ExpiresAt);
    }

    private async Task<RefreshToken?> FindTokenAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 256)
        {
            return null;
        }

        var hash = RefreshToken.Hash(rawToken);
        return await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevocation reason, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), cancellationToken);
    }

    private async Task RevokeUserSessionsAsync(Guid userId, Guid? keepFamilyId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.FamilyId != keepFamilyId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, RefreshTokenRevocation.PasswordChanged),
                cancellationToken);
    }

    /// <summary>Email links carry Identity tokens base64url-encoded.</summary>
    private static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string EncodeToken(string token) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
}
