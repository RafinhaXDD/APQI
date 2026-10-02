using System.Net;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Identity;

public sealed class LowRateLimitFixture(PostgisContainer postgis) : ApiFixture(postgis)
{
    protected override IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>
    {
        ["RateLimiting:AuthPermitLimit"] = "3",
        ["RateLimiting:RefreshPermitLimit"] = "3",
    };
}

/// <summary>R-19: login, register, forgot-password and refresh are rate-limited per client IP.</summary>
public sealed class RateLimitTests(LowRateLimitFixture fixture) : IClassFixture<LowRateLimitFixture>
{
    [Fact]
    public async Task Auth_endpoints_share_one_budget_and_refresh_has_its_own()
    {
        using var client = fixture.CreateAuthClient();
        var body = new { email = AuthClient.NewEmail(), password = AuthClient.DefaultPassword, displayName = "Ana" };

        // One shared "auth" budget of 3: an attacker can't multiply it by switching endpoints.
        foreach (var path in new[] { "/api/auth/login", "/api/auth/register", "/api/auth/forgot-password" })
        {
            using var allowed = await client.PostAsync(path, body);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, path);
        }

        foreach (var path in new[] { "/api/auth/login", "/api/auth/register", "/api/auth/forgot-password" })
        {
            using var limited = await client.PostAsync(path, body);
            await AssertLimitedAsync(limited);
        }

        // Refresh is unaffected by the exhausted auth budget, then hits its own limit.
        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.PostAsync("/api/auth/refresh", new { });
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no cookie, but not rate-limited");
        }

        using var refreshLimited = await client.PostAsync("/api/auth/refresh", new { });
        await AssertLimitedAsync(refreshLimited);
    }

    private static async Task AssertLimitedAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await response.ProblemCodeAsync()).Should().Be("rate_limited");
        response.Headers.RetryAfter.Should().NotBeNull();
    }
}
