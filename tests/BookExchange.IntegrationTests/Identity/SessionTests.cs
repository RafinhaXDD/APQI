using System.Net;
using System.Text.Json;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Identity;

public sealed class SessionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_returns_an_access_token_and_a_strict_http_only_refresh_cookie()
    {
        using var client = fixture.CreateAuthClient();
        var email = await client.SignUpAndLoginAsync(name: "Carla");

        using var me = await client.GetAsync("/api/auth/me");

        client.LastSetCookie.Should().NotBeNull();
        var cookie = client.LastSetCookie!.ToLowerInvariant();
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/api/auth");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("email").GetString().Should().Be(email);
        body.RootElement.GetProperty("displayName").GetString().Should().Be("Carla");
        body.RootElement.GetProperty("emailConfirmed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_identical_responses()
    {
        using var client = fixture.CreateAuthClient();
        var email = await client.SignUpAndLoginAsync();

        using var wrongPassword = await client.LoginAsync(email, "not the password");
        using var unknownEmail = await client.LoginAsync(AuthClient.NewEmail(), "not the password");

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrongPassword.ProblemCodeAsync()).Should().Be("auth.invalid_credentials");
        (await unknownEmail.ProblemCodeAsync()).Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        using var client = fixture.CreateAuthClient();
        var email = await client.SignUpAndLoginAsync();

        for (var i = 0; i < 5; i++)
        {
            (await client.LoginAsync(email, "wrong password " + i)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        using var right = await client.LoginAsync(email);

        right.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await right.ProblemCodeAsync()).Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_issues_a_working_access_token()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();
        var firstCookie = client.RefreshCookie;

        using var refreshed = await client.RefreshAsync();
        using var me = await client.GetAsync("/api/auth/me");

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        client.RefreshCookie.Should().NotBeNull().And.NotBe(firstCookie);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();
        var stolen = client.RefreshCookie;
        (await client.RefreshAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
        var legitimate = client.RefreshCookie;

        client.RefreshCookie = stolen;
        using var reuse = await client.RefreshAsync();
        client.RefreshCookie = legitimate;
        using var afterReuse = await client.RefreshAsync();

        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await reuse.ProblemCodeAsync()).Should().Be("auth.session_expired");
        afterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the newest token belongs to the revoked family");
    }

    [Fact]
    public async Task Refresh_without_or_with_a_garbage_cookie_is_401()
    {
        using var client = fixture.CreateAuthClient();

        using var none = await client.RefreshAsync();
        client.RefreshCookie = "garbage";
        using var garbage = await client.RefreshAsync();

        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        garbage.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_clears_the_cookie()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();
        var token = client.RefreshCookie;

        using var logout = await client.LogoutAsync();
        client.RefreshCookie = token;
        using var refresh = await client.RefreshAsync();

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        client.LastSetCookie.Should().NotBeNull();
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not.a.jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAifQ.c2lnbmF0dXJl")]
    public async Task Protected_endpoints_reject_missing_or_forged_tokens_with_problem_details(string? token)
    {
        using var client = fixture.CreateAuthClient();
        if (token is not null)
        {
            client.Http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        using var me = await client.Http.GetAsync(new Uri("/api/auth/me", UriKind.Relative), Ct);
        using var profile = await client.Http.GetAsync(new Uri("/api/users/me", UriKind.Relative), Ct);

        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        profile.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        me.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await me.ProblemCodeAsync()).Should().Be("auth.unauthorized");
    }

    [Fact]
    public async Task Auth_responses_carry_no_store_and_security_headers()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);
        await client.ConfirmFromEmailAsync(email);

        using var login = await client.LoginAsync(email);

        login.Headers.CacheControl!.NoStore.Should().BeTrue();
        login.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        login.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }
}
