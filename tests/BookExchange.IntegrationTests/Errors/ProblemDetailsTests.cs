using System.Net;
using System.Text.Json;
using BookExchange.Domain.Shared;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.IntegrationTests.Errors;

/// <summary>R-22: every error is RFC 7807 ProblemDetails; internals never leave the server.</summary>
public sealed class ProblemDetailsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Unknown_route_returns_404_problem_details_to_a_signed_in_user()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        body.RootElement.GetProperty("code").GetString().Should().Be("not_found");
        body.RootElement.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unknown_route_returns_401_problem_details_to_anonymous_callers()
    {
        // Secure-by-default fallback policy also covers unmatched routes: anonymous callers can't map the API.
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Domain_exception_maps_to_409_with_its_message()
    {
        var (status, body) = await HandleAsync(new DomainException("Listing is not active."));

        status.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("detail").GetString().Should().Be("Listing is not active.");
    }

    [Fact]
    public async Task Concurrency_exception_maps_to_409()
    {
        var (status, _) = await HandleAsync(new DbUpdateConcurrencyException("row version mismatch"));

        status.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Unexpected_exception_maps_to_500_without_leaking_details()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("Password=hunter2 at Some.Internal.Type"));

        status.Should().Be(StatusCodes.Status500InternalServerError);
        var json = body.GetRawText();
        json.Should().NotContain("hunter2").And.NotContain("Internal.Type").And.NotContain("stack", "no stack traces");
        body.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public async Task Client_disconnect_is_not_reported_as_a_server_error()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().Single();
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = aborted.Token };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(499);
    }

    private async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IExceptionHandler>().Single();
        using var responseBody = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = responseBody;

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        handled.Should().BeTrue();
        context.Response.ContentType.Should().StartWith("application/problem+json");
        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(responseBody, cancellationToken: TestContext.Current.CancellationToken);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }
}
