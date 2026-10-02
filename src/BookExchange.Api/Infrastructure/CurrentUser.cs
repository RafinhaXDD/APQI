using System.Security.Claims;
using BookExchange.Infrastructure.Identity;

namespace BookExchange.Api.Infrastructure;

internal static class CurrentUser
{
    /// <summary>The caller's user id from the validated access token (never from the request body, R-10).</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(AppClaims.Subject), out var id) ? id : null;

    public static Guid? GetSessionId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(AppClaims.SessionId), out var id) ? id : null;
}
