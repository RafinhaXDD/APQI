using System.Net;
using System.Net.Http.Json;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Identity;

public sealed class PasswordTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string NewPassword = "a brand new passphrase";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forgot_password_answers_identically_and_only_emails_existing_confirmed_accounts()
    {
        using var client = fixture.CreateAuthClient();
        var email = await client.SignUpAndLoginAsync();
        var unknown = AuthClient.NewEmail();
        var before = fixture.Factory.Emails.To(email).Count;

        using var known = await client.PostAsync("/api/auth/forgot-password", new { email });
        using var missing = await client.PostAsync("/api/auth/forgot-password", new { email = unknown });
        await fixture.RunOutboxAsync();

        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        missing.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await known.Content.ReadAsStringAsync(Ct)).Should().Be(await missing.Content.ReadAsStringAsync(Ct));
        fixture.Factory.Emails.To(email).Count.Should().Be(before + 1);
        fixture.Factory.Emails.To(unknown).Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_password_sets_the_new_password_and_signs_out_every_session()
    {
        using var client = fixture.CreateAuthClient();
        var email = await client.SignUpAndLoginAsync();
        await client.PostAsync("/api/auth/forgot-password", new { email });
        await fixture.RunOutboxAsync();
        var (userId, token) = fixture.Factory.Emails.LatestLink(email, "/reset-password");

        using var reset = await client.Http.PostAsJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = NewPassword }, Ct);
        using var oldSession = await client.RefreshAsync();
        using var oldPassword = await client.LoginAsync(email);
        using var newPassword = await client.LoginAsync(email, NewPassword);
        using var reused = await client.Http.PostAsJsonAsync("/api/auth/reset-password", new { userId, token, newPassword = "yet another passphrase" }, Ct);

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        oldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newPassword.StatusCode.Should().Be(HttpStatusCode.OK);
        reused.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a reset link works once (security stamp changed)");
    }

    [Fact]
    public async Task Reset_with_a_bad_token_is_an_invalid_link()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.Http.PostAsJsonAsync(
            "/api/auth/reset-password", new { userId = Guid.NewGuid(), token = "AAAA", newPassword = NewPassword }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).Should().Be("auth.invalid_link");
    }

    [Fact]
    public async Task Change_password_rejects_a_wrong_current_password()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.PostAsync("/api/auth/change-password", new { currentPassword = "not it at all", newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("auth.wrong_password");
    }

    [Fact]
    public async Task Change_password_keeps_the_current_session_and_signs_out_the_others()
    {
        var email = AuthClient.NewEmail();
        using var phone = fixture.CreateAuthClient();
        using var laptop = fixture.CreateAuthClient();
        await phone.SignUpAndLoginAsync(email);
        await laptop.LoginAsync(email);

        using var change = await phone.PostAsync("/api/auth/change-password", new { currentPassword = AuthClient.DefaultPassword, newPassword = NewPassword });
        using var phoneRefresh = await phone.RefreshAsync();
        using var laptopRefresh = await laptop.RefreshAsync();

        change.StatusCode.Should().Be(HttpStatusCode.NoContent);
        phoneRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        laptopRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Change_password_requires_authentication()
    {
        using var client = fixture.CreateAuthClient();

        using var response = await client.PostAsync("/api/auth/change-password", new { currentPassword = "x", newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
