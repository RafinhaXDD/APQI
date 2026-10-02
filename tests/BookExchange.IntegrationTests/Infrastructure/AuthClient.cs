using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>
/// Test client that behaves like the SPA: keeps the access token in memory and the refresh cookie
/// itself (HttpClient won't send a Secure cookie over the test server's http://).
/// </summary>
public sealed class AuthClient(HttpClient http, ApiFixture fixture) : IDisposable
{
    public const string DefaultPassword = "correct horse battery";

    public HttpClient Http => http;

    public string? AccessToken { get; private set; }

    public string? RefreshCookie { get; set; }

    /// <summary>The last Set-Cookie for the refresh token, attributes included.</summary>
    public string? LastSetCookie { get; private set; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    public Task<HttpResponseMessage> RegisterAsync(string email, string password = DefaultPassword, string name = "Ana Leitora", string? language = null) =>
        http.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = name, preferredLanguage = language }, Ct);

    public async Task<HttpResponseMessage> ConfirmFromEmailAsync(string email)
    {
        await fixture.RunOutboxAsync();
        var (userId, token) = fixture.Factory.Emails.LatestLink(email, "/confirm-email");
        return await http.PostAsJsonAsync("/api/auth/confirm-email", new { userId, token }, Ct);
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password = DefaultPassword)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);
        await CaptureSessionAsync(response);
        return response;
    }

    /// <summary>Register + confirm + login in one go; returns the email used.</summary>
    public async Task<string> SignUpAndLoginAsync(string? email = null, string password = DefaultPassword, string name = "Ana Leitora")
    {
        email ??= NewEmail();
        (await RegisterAsync(email, password, name)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ConfirmFromEmailAsync(email)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LoginAsync(email, password)).StatusCode.Should().Be(HttpStatusCode.OK);
        return email;
    }

    public async Task<HttpResponseMessage> RefreshAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        AddCookie(request);
        var response = await http.SendAsync(request, Ct);
        await CaptureSessionAsync(response);
        return response;
    }

    public async Task<HttpResponseMessage> LogoutAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        AddCookie(request);
        var response = await http.SendAsync(request, Ct);
        CaptureCookie(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, body: null);

    public Task<HttpResponseMessage> PostAsync(string path, object body) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (AccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(request, Ct);
    }

    public void Dispose() => http.Dispose();

    private void AddCookie(HttpRequestMessage request)
    {
        if (RefreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"aqpi_refresh={RefreshCookie}");
        }
    }

    private async Task CaptureSessionAsync(HttpResponseMessage response)
    {
        CaptureCookie(response);
        if (response.IsSuccessStatusCode)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            AccessToken = body.RootElement.GetProperty("accessToken").GetString();
        }
    }

    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        var refresh = cookies.LastOrDefault(c => c.StartsWith("aqpi_refresh=", StringComparison.Ordinal));
        if (refresh is null)
        {
            return;
        }

        LastSetCookie = refresh;
        var value = refresh.Split(';')[0]["aqpi_refresh=".Length..];
        RefreshCookie = value.Length == 0 ? null : value;
    }
}

public static class ProblemExtensions
{
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
