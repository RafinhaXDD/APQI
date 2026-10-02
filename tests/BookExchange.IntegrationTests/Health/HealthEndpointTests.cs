using System.Net;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Health;

public sealed class HealthEndpointTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task Health_endpoints_are_green_when_the_database_is_reachable(string path)
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Readiness_is_unhealthy_but_liveness_is_ok_when_the_database_is_unreachable()
    {
        // Nothing listens on port 1; short timeout keeps the test fast.
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=2");
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
