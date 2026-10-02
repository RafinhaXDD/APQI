using System.Globalization;
using System.Threading.RateLimiting;
using BookExchange.Application.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BookExchange.Api.Infrastructure;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Login, register, password and confirmation endpoints, per client IP.</summary>
    public int AuthPermitLimit { get; set; } = 20;

    public TimeSpan AuthWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Token refresh runs on every page load, so it gets a larger budget.</summary>
    public int RefreshPermitLimit { get; set; } = 60;

    public TimeSpan RefreshWindow { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>Built-in ASP.NET Core rate limiter (ADR-05, R-19); single instance, so in-memory state is enough.</summary>
internal static class RateLimiting
{
    public const string Auth = "auth";
    public const string Refresh = "auth-refresh";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Auth, http => FixedWindow(http, Auth, settings.AuthPermitLimit, settings.AuthWindow));
            options.AddPolicy(Refresh, http => FixedWindow(http, Refresh, settings.RefreshPermitLimit, settings.RefreshWindow));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests. Try again shortly.",
                        Extensions = { [Problems.CodeKey] = ErrorCodes.RateLimited },
                    },
                });
            };
        });
    }

    // Partitioned by client IP. Behind Azure's proxy this needs forwarded headers (Phase 15).
    private static RateLimitPartition<string> FixedWindow(HttpContext http, string policy, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });
}
