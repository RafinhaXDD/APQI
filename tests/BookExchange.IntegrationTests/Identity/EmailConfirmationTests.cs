using System.Net;
using System.Net.Http.Json;
using BookExchange.Domain.Credits;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Identity;

public sealed class EmailConfirmationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Confirming_grants_exactly_one_starter_credit_and_the_balance_equals_the_event_sum()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);

        using var first = await client.ConfirmFromEmailAsync(email);
        var (userId, token) = fixture.Factory.Emails.LatestLink(email, "/confirm-email");
        using var again = await client.Http.PostAsJsonAsync("/api/auth/confirm-email", new { userId, token }, Ct);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        again.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.BadRequest);
        await AssertSingleStarterAsync(userId);
    }

    [Fact]
    public async Task Concurrent_confirmations_still_grant_a_single_starter_credit()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);
        await fixture.RunOutboxAsync();
        var (userId, token) = fixture.Factory.Emails.LatestLink(email, "/confirm-email");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/confirm-email", new { userId, token }, Ct)));

        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.NoContent);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent || r.StatusCode == HttpStatusCode.BadRequest);
        await AssertSingleStarterAsync(userId);
    }

    [Fact]
    public async Task A_tampered_token_or_unknown_user_is_an_invalid_link()
    {
        using var client = fixture.CreateAuthClient();
        var email = AuthClient.NewEmail();
        await client.RegisterAsync(email);
        await fixture.RunOutboxAsync();
        var (userId, token) = fixture.Factory.Emails.LatestLink(email, "/confirm-email");

        using var tampered = await client.Http.PostAsJsonAsync("/api/auth/confirm-email", new { userId, token = token[..^4] + "AAAA" }, Ct);
        using var unknownUser = await client.Http.PostAsJsonAsync("/api/auth/confirm-email", new { userId = Guid.NewGuid(), token }, Ct);
        using var notBase64 = await client.Http.PostAsJsonAsync("/api/auth/confirm-email", new { userId, token = "***" }, Ct);

        foreach (var response in new[] { tampered, unknownUser, notBase64 })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.ProblemCodeAsync()).Should().Be("auth.invalid_link");
        }

        (await fixture.WithDbAsync(db => db.CreditEvents.CountAsync(e => e.UserId == userId, Ct))).Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_email_is_in_the_users_language()
    {
        using var client = fixture.CreateAuthClient();
        var english = AuthClient.NewEmail();
        var portuguese = AuthClient.NewEmail();

        await client.RegisterAsync(english, language: "en");
        await client.RegisterAsync(portuguese);
        await fixture.RunOutboxAsync();

        fixture.Factory.Emails.To(english).Single().Subject.Should().Be("Confirm your AQPI email");
        fixture.Factory.Emails.To(portuguese).Single().Subject.Should().Be("Confirme seu e-mail na AQPI");
    }

    private async Task AssertSingleStarterAsync(Guid userId)
    {
        var (starters, sum, account) = await fixture.WithDbAsync(async db => (
            await db.CreditEvents.CountAsync(e => e.UserId == userId && e.Type == CreditEventType.Starter, Ct),
            await db.CreditEvents.Where(e => e.UserId == userId).SumAsync(e => e.Amount, Ct),
            await db.CreditAccounts.AsNoTracking().SingleAsync(a => a.UserId == userId, Ct)));

        starters.Should().Be(1);
        account.Available.Should().Be(sum).And.Be(1);
        account.Held.Should().Be(0);
    }
}
