using System.Net;
using System.Text.Json;
using BookExchange.Domain.Shared;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Listings;

/// <summary>
/// Radius + full-text search (SPEC §6.4). Tests share one database, so each test works around its own
/// origin, hundreds of kilometres from the others.
/// </summary>
public sealed class ListingSearchTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static GeoPoint OriginFor(int test) => PublicLocation.Offset(CatalogHelpers.Center, test * 300_000, 200);

    private static string Search(GeoPoint origin, string query = "") =>
        $"/api/listings?lat={origin.Latitude}&lng={origin.Longitude}{query}";

    [Fact]
    public async Task Radius_filters_and_results_are_sorted_by_distance()
    {
        var origin = OriginFor(1);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var at2km = await owner.CreateListingAsync(title: "Perto", at: PublicLocation.Offset(origin, 2000, 45));
        var at4km = await owner.CreateListingAsync(title: "Médio", at: PublicLocation.Offset(origin, 4000, 180));
        var at9km = await owner.CreateListingAsync(title: "Longe", at: PublicLocation.Offset(origin, 9000, 270));
        using var anonymous = fixture.CreateAuthClient();

        var within5 = await IdsAsync(await anonymous.GetAsync(Search(origin, "&radiusKm=5")));
        var within15 = await IdsAsync(await anonymous.GetAsync(Search(origin, "&radiusKm=15")));

        within5.Should().Equal(at2km, at4km);
        within15.Should().Equal(at2km, at4km, at9km);
    }

    [Theory]
    [InlineData("jose saramago")]
    [InlineData("SARAMAGO")]
    [InlineData("cegueira")]
    [InlineData("ensaio sar")]
    public async Task Full_text_is_accent_and_case_insensitive_with_prefix_matching(string query)
    {
        var origin = OriginFor(2);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var saramago = await owner.CreateListingAsync(title: "Ensaio sobre a Cegueira", authors: ["José Saramago"], at: origin);
        await owner.CreateListingAsync(title: "O Senhor dos Anéis", authors: ["J. R. R. Tolkien"], at: origin);
        using var anonymous = fixture.CreateAuthClient();

        using var response = await anonymous.GetAsync(Search(origin, $"&q={Uri.EscapeDataString(query)}"));
        using var body = await response.JsonAsync();
        var found = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => (Id: e.GetProperty("id").GetGuid(), Title: e.GetProperty("title").GetString())).ToList();

        // Cases of this theory share the origin, so earlier cases' copies may appear too: all must be Saramago.
        found.Select(f => f.Id).Should().Contain(saramago);
        found.Should().OnlyContain(f => f.Title == "Ensaio sobre a Cegueira");
    }

    [Theory]
    [InlineData("a & ! | :* (")]
    [InlineData("'; DROP TABLE \"Listings\"; --")]
    [InlineData("&&&")]
    public async Task Query_syntax_from_the_caller_never_breaks_the_search(string query)
    {
        var origin = OriginFor(3);
        using var anonymous = fixture.CreateAuthClient();

        using var response = await anonymous.GetAsync(Search(origin, $"&q={Uri.EscapeDataString(query)}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Isbn_and_condition_filters_narrow_the_results()
    {
        var origin = OriginFor(4);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var withIsbn = await owner.CreateListingAsync(title: "Capitães da Areia", isbn: "9788535914849", at: origin, condition: "New");
        var worn = await owner.CreateListingAsync(title: "Gabriela", at: origin, condition: "Worn");
        using var anonymous = fixture.CreateAuthClient();

        (await IdsAsync(await anonymous.GetAsync(Search(origin, "&isbn=978-85-359-1484-9")))).Should().Equal(withIsbn);
        (await IdsAsync(await anonymous.GetAsync(Search(origin, "&condition=Worn")))).Should().Equal(worn);
    }

    [Fact]
    public async Task Category_and_good_for_beginners_filters_narrow_the_results()
    {
        var origin = OriginFor(10);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var novel = await owner.CreateListingAsync(title: "O Pequeno Príncipe", at: origin, category: "Fiction", goodForBeginners: true);
        var philosophy = await owner.CreateListingAsync(title: "Meditações", at: origin, category: "Philosophy");
        var career = await owner.CreateListingAsync(title: "A Arte da Guerra", at: origin, category: "CareerStrategy", goodForBeginners: true);
        using var anonymous = fixture.CreateAuthClient();

        (await IdsAsync(await anonymous.GetAsync(Search(origin, "&category=Philosophy")))).Should().Equal(philosophy);
        (await IdsAsync(await anonymous.GetAsync(Search(origin, "&beginners=true")))).Should().BeEquivalentTo([novel, career]);
        (await IdsAsync(await anonymous.GetAsync(Search(origin, "&category=Fiction&beginners=true")))).Should().Equal(novel);
        using var body = await (await anonymous.GetAsync(Search(origin, "&category=CareerStrategy"))).JsonAsync();
        var item = body.RootElement.GetProperty("items")[0];
        item.GetProperty("category").GetString().Should().Be("CareerStrategy");
        item.GetProperty("goodForBeginners").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Only_active_listings_are_returned()
    {
        var origin = OriginFor(5);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var active = await owner.CreateListingAsync(title: "Ativo", at: origin);
        var archived = await owner.CreateListingAsync(title: "Arquivado", at: origin);
        await owner.PostAsync($"/api/listings/{archived}/archive", new { });
        using var anonymous = fixture.CreateAuthClient();

        (await IdsAsync(await anonymous.GetAsync(Search(origin)))).Should().Equal(active);
    }

    [Fact]
    public async Task Anonymous_search_needs_a_location_and_signed_in_search_defaults_to_home()
    {
        var origin = OriginFor(6);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var mine = await owner.CreateListingAsync(at: origin);
        using var anonymous = fixture.CreateAuthClient();

        using var noLocation = await anonymous.GetAsync("/api/listings");
        var fromHome = await IdsAsync(await owner.GetAsync("/api/listings"));

        noLocation.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await noLocation.ProblemCodeAsync()).Should().Be("listing.location_required");
        fromHome.Should().Contain(mine);
    }

    [Fact]
    public async Task Page_size_is_capped_at_50_and_paging_reports_the_total()
    {
        var origin = OriginFor(7);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        for (var i = 0; i < 3; i++)
        {
            await owner.CreateListingAsync(title: $"Livro {i}", at: origin);
        }

        using var anonymous = fixture.CreateAuthClient();
        using var capped = await (await anonymous.GetAsync(Search(origin, "&pageSize=500"))).JsonAsync();
        using var page2 = await (await anonymous.GetAsync(Search(origin, "&pageSize=2&page=2"))).JsonAsync();

        capped.RootElement.GetProperty("pageSize").GetInt32().Should().Be(50);
        page2.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        page2.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Coordinates_are_never_exposed_in_search_or_details_and_distances_are_rounded()
    {
        var origin = OriginFor(8);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var nearby = await owner.CreateListingAsync(title: "Pertinho", at: PublicLocation.Offset(origin, 150, 0));
        var farther = await owner.CreateListingAsync(title: "Mais longe", at: PublicLocation.Offset(origin, 3300, 90));
        using var anonymous = fixture.CreateAuthClient();

        using var search = await anonymous.GetAsync(Search(origin));
        using var details = await anonymous.GetAsync($"/api/listings/{farther}?lat={origin.Latitude}&lng={origin.Longitude}");
        var searchJson = await search.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var detailsJson = await details.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        foreach (var json in new[] { searchJson, detailsJson })
        {
            AssertNoCoordinates(json);
        }

        using var parsed = JsonDocument.Parse(searchJson);
        var items = parsed.RootElement.GetProperty("items").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetGuid());
        items[nearby].GetProperty("distanceKm").GetDouble().Should().Be(1);
        items[nearby].GetProperty("distanceIsUpperBound").GetBoolean().Should().BeTrue("150 m + 300–700 m jitter is under 1 km");
        var km = items[farther].GetProperty("distanceKm").GetDouble();
        (km * 2).Should().Be(Math.Floor(km * 2), "distances are rounded to 0.5 km");
        km.Should().BeInRange(2.5, 4);
    }

    [Fact]
    public async Task Repeated_searches_return_the_same_distance_because_the_jitter_is_fixed()
    {
        var origin = OriginFor(9);
        using var owner = await fixture.SignedInWithHomeAsync(origin);
        var id = await owner.CreateListingAsync(at: PublicLocation.Offset(origin, 2700, 10));
        using var anonymous = fixture.CreateAuthClient();

        var distances = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            using var body = await (await anonymous.GetAsync(Search(origin))).JsonAsync();
            distances.Add(body.RootElement.GetProperty("items").EnumerateArray()
                .Single(e => e.GetProperty("id").GetGuid() == id).GetProperty("distanceKm").GetDouble());
        }

        distances.Distinct().Should().ContainSingle();
    }

    private static void AssertNoCoordinates(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = new List<string>();
        Collect(document.RootElement, names);
        string[] forbidden = ["latitude", "longitude", "lat", "lng", "lon", "location", "publicPoint", "myLocation", "x", "y"];
        names.Should().NotContain(n => forbidden.Contains(n, StringComparer.OrdinalIgnoreCase));
    }

    private static void Collect(JsonElement element, List<string> names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                // myLocation is present only for the owner and is null for everyone else.
                if (property.Name == "myLocation" && property.Value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                names.Add(property.Name);
                Collect(property.Value, names);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Collect(item, names);
            }
        }
    }

    private static async Task<List<Guid>> IdsAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var body = await response.JsonAsync();
            return body.RootElement.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        }
    }
}
