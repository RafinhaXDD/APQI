using BookExchange.Application.Users;
using FluentValidation;

namespace BookExchange.Application.Auth;

/// <summary>Mirrored by the Identity password options and by the frontend form rules.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 128;
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string? PreferredLanguage);

public sealed record LoginRequest(string Email, string Password);

/// <summary>Body of forgot-password and resend-confirmation.</summary>
public sealed record EmailRequest(string Email);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>The access token is returned in the body and kept in memory by the client (R-21).</summary>
public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record MeResponse(Guid Id, string Email, bool EmailConfirmed, string DisplayName, string PreferredLanguage);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).Email();
        RuleFor(r => r.Password).Password();
        RuleFor(r => r.DisplayName).DisplayName();
        RuleFor(r => r.PreferredLanguage!).Language().When(r => r.PreferredLanguage is not null);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Only shape checks: a policy check here would tell callers which passwords can't exist.
        RuleFor(r => r.Email).NotEmpty().MaximumLength(AuthRules.EmailMaxLength);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
    }
}

public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(r => r.Email).Email();
}

public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty().MaximumLength(AuthRules.TokenMaxLength);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(r => r.UserId).NotEmpty();
        RuleFor(r => r.Token).NotEmpty().MaximumLength(AuthRules.TokenMaxLength);
        RuleFor(r => r.NewPassword).Password();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
        RuleFor(r => r.NewPassword).Password();
    }
}

public static class AuthRules
{
    public const int EmailMaxLength = 254;
    public const int TokenMaxLength = 2048;

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(EmailMaxLength).EmailAddress();

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordPolicy.MinLength).MaximumLength(PasswordPolicy.MaxLength);
}
