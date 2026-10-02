using System.Security.Claims;
using BookExchange.Api.Infrastructure;
using BookExchange.Application.Auth;
using BookExchange.Application.Shared;
using BookExchange.Infrastructure.Identity;

namespace BookExchange.Api.Auth;

/// <summary>SPEC §8 endpoints under /api/auth.</summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Identical 202 whether or not the email exists (R-19).
        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
                Respond(await auth.RegisterAsync(request, ct), _ => Results.Accepted()))
            .AllowAnonymous().Validate<RegisterRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/login", async (LoginRequest request, AuthService auth, HttpResponse response, CancellationToken ct) =>
                Respond(await auth.LoginAsync(request, ct), tokens => StartSession(response, tokens)))
            .AllowAnonymous().Validate<LoginRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/refresh", async (HttpRequest request, HttpResponse response, AuthService auth, CancellationToken ct) =>
            {
                var result = await auth.RefreshAsync(RefreshCookie.Read(request), ct);
                if (!result.Succeeded)
                {
                    RefreshCookie.Clear(response);
                }

                return Respond(result, tokens => StartSession(response, tokens));
            })
            .AllowAnonymous().RequireRateLimiting(RateLimiting.Refresh);

        // Anonymous on purpose: logging out must work after the access token expired.
        group.MapPost("/logout", async (HttpRequest request, HttpResponse response, AuthService auth, CancellationToken ct) =>
            {
                await auth.LogoutAsync(RefreshCookie.Read(request), ct);
                RefreshCookie.Clear(response);
                return Results.NoContent();
            })
            .AllowAnonymous();

        group.MapPost("/confirm-email", async (ConfirmEmailRequest request, AuthService auth, CancellationToken ct) =>
                Respond(await auth.ConfirmEmailAsync(request, ct), _ => Results.NoContent()))
            .AllowAnonymous().Validate<ConfirmEmailRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/resend-confirmation", async (EmailRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.ResendConfirmationAsync(request, ct);
                return Results.Accepted();
            })
            .AllowAnonymous().Validate<EmailRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/forgot-password", async (EmailRequest request, AuthService auth, CancellationToken ct) =>
            {
                await auth.ForgotPasswordAsync(request, ct);
                return Results.Accepted();
            })
            .AllowAnonymous().Validate<EmailRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AuthService auth, CancellationToken ct) =>
                Respond(await auth.ResetPasswordAsync(request, ct), _ => Results.NoContent(), passwordField: "newPassword"))
            .AllowAnonymous().Validate<ResetPasswordRequest>().RequireRateLimiting(RateLimiting.Auth);

        group.MapGet("/me", async (ClaimsPrincipal principal, AuthService auth, CancellationToken ct) =>
            principal.GetUserId() is { } userId && await auth.GetMeAsync(userId, ct) is { } me
                ? Results.Ok(me)
                : Problems.Status(StatusCodes.Status401Unauthorized, "Sign in again.", ErrorCodes.Unauthorized));

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, AuthService auth, CancellationToken ct) =>
            {
                if (principal.GetUserId() is not { } userId)
                {
                    return Problems.Status(StatusCodes.Status401Unauthorized, "Sign in again.", ErrorCodes.Unauthorized);
                }

                var result = await auth.ChangePasswordAsync(userId, principal.GetSessionId(), request, ct);
                return Respond(result, _ => Results.NoContent(), passwordField: "newPassword");
            })
            .Validate<ChangePasswordRequest>().RequireRateLimiting(RateLimiting.Auth);

        return app;
    }

    private static IResult StartSession(HttpResponse response, SessionTokens tokens)
    {
        RefreshCookie.Set(response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        response.Headers.CacheControl = "no-store";
        return Results.Ok(new AccessTokenResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt));
    }

    private static IResult Respond<T>(AuthResult<T> result, Func<T, IResult> onSuccess, string passwordField = "password") =>
        result.Failure switch
        {
            null => onSuccess(result.Value!),
            AuthFailure.InvalidCredentials => Problems.Status(StatusCodes.Status401Unauthorized, "Email or password is incorrect.", ErrorCodes.InvalidCredentials),
            AuthFailure.EmailNotConfirmed => Problems.Status(StatusCodes.Status403Forbidden, "Confirm your email before signing in.", ErrorCodes.EmailNotConfirmed),
            AuthFailure.SessionExpired => Problems.Status(StatusCodes.Status401Unauthorized, "Your session has expired. Sign in again.", ErrorCodes.SessionExpired),
            AuthFailure.InvalidLink => Problems.Status(StatusCodes.Status400BadRequest, "This link is invalid or has expired.", ErrorCodes.InvalidLink),
            AuthFailure.WrongPassword => Problems.Validation(
                new Dictionary<string, string[]> { ["currentPassword"] = ["The current password is incorrect."] }, ErrorCodes.WrongPassword),
            AuthFailure.PasswordRejected => Problems.Validation(new Dictionary<string, string[]> { [passwordField] = [.. result.Errors] }),
            _ => throw new InvalidOperationException($"Unhandled auth failure {result.Failure}."),
        };
}
