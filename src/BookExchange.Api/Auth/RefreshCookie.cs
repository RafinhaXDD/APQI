namespace BookExchange.Api.Auth;

/// <summary>
/// Refresh token transport (R-21): HttpOnly, Secure, SameSite=Strict, and only sent to /api/auth.
/// The SPA and API share one origin (Vite proxy in dev, reverse proxy in prod), so Strict works.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "aqpi_refresh";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Set(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Clear(HttpResponse response) => response.Cookies.Delete(Name, Options(expiresAt: null));

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
