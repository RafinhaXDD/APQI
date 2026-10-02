using System.Net;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Identity;

public sealed class RegistrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_accepts_and_sends_a_confirmation_email()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();

        using var response = await client.RegisterAsync(email);
        await fixture.RunOutboxAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        fixture.Factory.Emails.To(email).Should().ContainSingle()
            .Which.TextBody.Should().Contain("http://app.test/confirm-email?userId=");
    }

    [Fact]
    public async Task Register_with_an_existing_email_answers_identically_and_warns_the_owner_by_email()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        using var first = await client.RegisterAsync(email);

        using var second = await client.RegisterAsync(email.ToUpperInvariant(), name: "Someone Else");
        await fixture.RunOutboxAsync();

        second.StatusCode.Should().Be(first.StatusCode);
        (await second.Content.ReadAsStringAsync(Ct)).Should().Be(await first.Content.ReadAsStringAsync(Ct));
        fixture.Factory.Emails.To(email).Select(m => m.Subject).Should().HaveCount(2)
            .And.Contain(s => s.Contains("alguém tentou", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_creates_profile_and_empty_credit_account_but_no_credit_before_confirmation()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();

        await client.RegisterAsync(email, name: "  Bruno  ", language: "en");

        var (profile, account, events) = await fixture.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email, Ct);
            return (
                await db.UserProfiles.SingleAsync(p => p.UserId == user.Id, Ct),
                await db.CreditAccounts.SingleAsync(a => a.UserId == user.Id, Ct),
                await db.CreditEvents.CountAsync(e => e.UserId == user.Id, Ct));
        });
        profile.DisplayName.Should().Be("Bruno");
        profile.PreferredLanguage.Should().Be("en");
        account.Available.Should().Be(0);
        events.Should().Be(0);
    }

    [Theory]
    [InlineData("not-an-email", AuthClient.DefaultPassword, "Ana", "email")]
    [InlineData("ok@example.test", "short", "Ana", "password")]
    [InlineData("ok@example.test", AuthClient.DefaultPassword, "A", "displayName")]
    public async Task Invalid_registration_returns_422_with_the_field(string email, string password, string name, string field)
    {
        using var client = fixture.CreateAuthClient();

        using var response = await client.RegisterAsync(email, password, name);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("validation.failed");
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain($"\"{field}\"");
    }

    [Fact]
    public async Task Unsupported_language_is_rejected()
    {
        using var client = fixture.CreateAuthClient();

        using var response = await client.RegisterAsync(AuthClient.NewEmail(), language: "fr");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_before_confirming_is_refused_with_email_not_confirmed_only_for_the_right_password()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);

        using var right = await client.LoginAsync(email);
        using var wrong = await client.LoginAsync(email, "wrong password!!");

        right.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await right.ProblemCodeAsync()).Should().Be("auth.email_not_confirmed");
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrong.ProblemCodeAsync()).Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Resend_confirmation_answers_identically_for_unknown_and_unconfirmed_emails()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);
        await fixture.RunOutboxAsync();

        using var known = await client.PostAsync("/api/auth/resend-confirmation", new { email });
        using var unknown = await client.PostAsync("/api/auth/resend-confirmation", new { email = AuthClient.NewEmail() });
        await fixture.RunOutboxAsync();

        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        fixture.Factory.Emails.To(email).Should().HaveCount(2);
    }
}
