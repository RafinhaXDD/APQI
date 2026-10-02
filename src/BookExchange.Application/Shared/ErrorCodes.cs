namespace BookExchange.Application.Shared;

/// <summary>
/// Stable machine-readable error codes, returned as the <c>code</c> extension of ProblemDetails.
/// The frontend translates these; server titles are English fallbacks only.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string Unauthorized = "auth.unauthorized";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string EmailNotConfirmed = "auth.email_not_confirmed";
    public const string SessionExpired = "auth.session_expired";
    public const string InvalidLink = "auth.invalid_link";
    public const string WrongPassword = "auth.wrong_password";
    public const string RateLimited = "rate_limited";
    public const string Conflict = "conflict";
    public const string NotFound = "not_found";
    public const string ServerError = "server_error";
}
