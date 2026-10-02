using System.Net;
using System.Text.Json;
using BookExchange.IntegrationTests.Infrastructure;

namespace BookExchange.IntegrationTests.Users;

public sealed class ProfileTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task New_confirmed_user_has_one_available_credit_and_no_home_area()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync(name: "Dani");

        using var response = await client.GetAsync("/api/users/me");
        using var body = await ReadAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("displayName").GetString().Should().Be("Dani");
        body.RootElement.GetProperty("preferredLanguage").GetString().Should().Be("pt-BR");
        body.RootElement.GetProperty("homeArea").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("credits").GetProperty("available").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("credits").GetProperty("held").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Owner_can_set_and_read_back_their_exact_home_area()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var put = await client.PutAsync("/api/users/me/home-area", new { label = "Campus Norte", latitude = -23.5614, longitude = -46.7256 });
        using var get = await client.GetAsync("/api/users/me");
        using var body = await ReadAsync(get);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var area = body.RootElement.GetProperty("homeArea");
        area.GetProperty("label").GetString().Should().Be("Campus Norte");
        area.GetProperty("latitude").GetDouble().Should().BeApproximately(-23.5614, 1e-9);
        area.GetProperty("longitude").GetDouble().Should().BeApproximately(-46.7256, 1e-9);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task Out_of_range_coordinates_are_rejected(double latitude, double longitude)
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.PutAsync("/api/users/me/home-area", new { label = "Somewhere", latitude, longitude });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Profile_update_changes_name_bio_and_language()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var put = await client.PutAsync("/api/users/me", new { displayName = "Eva", bio = "Lê ficção científica.", preferredLanguage = "en" });
        using var body = await ReadAsync(put);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("displayName").GetString().Should().Be("Eva");
        body.RootElement.GetProperty("bio").GetString().Should().Be("Lê ficção científica.");
        body.RootElement.GetProperty("preferredLanguage").GetString().Should().Be("en");
    }

    [Fact]
    public async Task Profile_update_ignores_server_controlled_fields_in_the_body()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        // Mass assignment (SPEC §10.2): extra fields like userId or credits must have no effect.
        using var put = await client.PutAsync("/api/users/me", new
        {
            displayName = "Fábio",
            preferredLanguage = "pt-BR",
            userId = Guid.NewGuid(),
            credits = new { available = 999, held = 0 },
        });
        using var body = await ReadAsync(put);

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("credits").GetProperty("available").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Invalid_profile_values_are_rejected()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var badLanguage = await client.PutAsync("/api/users/me", new { displayName = "Gabi", preferredLanguage = "de" });
        using var longBio = await client.PutAsync("/api/users/me", new { displayName = "Gabi", bio = new string('x', 501), preferredLanguage = "en" });

        badLanguage.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        longBio.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
}
