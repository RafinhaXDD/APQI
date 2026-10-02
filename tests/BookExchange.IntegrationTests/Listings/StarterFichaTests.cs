using System.Net;
using BookExchange.Domain.Credits;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Listings;

/// <summary>
/// CLAUDE.md (Phase 5b): no ficha for signing up; the one starter ficha arrives with the first published book
/// that has a photo. "Give to get" — and the only way fichas enter the peer-to-peer side of the system.
/// </summary>
public sealed class StarterFichaTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_listing_without_a_photo_earns_nothing()
    {
        using var owner = await fixture.SignedInWithHomeAsync();

        await owner.CreateListingAsync();

        (await AvailableAsync(owner)).Should().Be(0);
    }

    [Fact]
    public async Task The_first_photo_on_a_listing_grants_exactly_one_starter_ficha_ever()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var first = await owner.CreateListingAsync(title: "Primeiro");
        var second = await owner.CreateListingAsync(title: "Segundo");

        (await owner.UploadImageAsync(first, TestImages.Jpeg(200, 150))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await owner.UploadImageAsync(first, TestImages.Jpeg(200, 150))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await owner.UploadImageAsync(second, TestImages.Jpeg(200, 150))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await AvailableAsync(owner)).Should().Be(1);
        await AssertLedgerAsync(owner, starters: 1, balance: 1);
    }

    [Fact]
    public async Task Archiving_and_relisting_cannot_farm_more_fichas()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var first = await owner.CreateListingAsync();
        await owner.UploadImageAsync(first, TestImages.Jpeg(200, 150));
        await owner.PostAsync($"/api/listings/{first}/archive", new { });

        var again = await owner.CreateListingAsync();
        await owner.UploadImageAsync(again, TestImages.Jpeg(200, 150));

        await AssertLedgerAsync(owner, starters: 1, balance: 1);
    }

    [Fact]
    public async Task Simultaneous_first_photos_still_grant_a_single_ficha()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var listings = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            listings.Add(await owner.CreateListingAsync(title: $"Livro {i}"));
        }

        var uploads = await Task.WhenAll(listings.Select(id => owner.UploadImageAsync(id, TestImages.Jpeg(200, 150))));

        uploads.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        await AssertLedgerAsync(owner, starters: 1, balance: 1);
    }

    private static async Task<int> AvailableAsync(AuthClient client)
    {
        using var body = await (await client.GetAsync("/api/users/me")).JsonAsync();
        return body.RootElement.GetProperty("credits").GetProperty("available").GetInt32();
    }

    private async Task AssertLedgerAsync(AuthClient client, int starters, int balance)
    {
        using var me = await (await client.GetAsync("/api/auth/me")).JsonAsync();
        var userId = me.RootElement.GetProperty("id").GetGuid();

        var (starterCount, sum, account) = await fixture.WithDbAsync(async db => (
            await db.CreditEvents.CountAsync(e => e.UserId == userId && e.Type == CreditEventType.Starter, Ct),
            await db.CreditEvents.Where(e => e.UserId == userId).SumAsync(e => e.Amount, Ct),
            await db.CreditAccounts.AsNoTracking().SingleAsync(a => a.UserId == userId, Ct)));

        starterCount.Should().Be(starters);
        account.Available.Should().Be(sum).And.Be(balance, "the cached balance always equals the event sum (R-13)");
    }
}
