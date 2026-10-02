using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BookExchange.Domain.Shared;

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>Shared steps for listing tests: a signed-in user with a home area, and listings created through the API.</summary>
public static class CatalogHelpers
{
    /// <summary>Praça da Sé, São Paulo: the origin used by distance tests.</summary>
    public static readonly GeoPoint Center = new(-23.5503, -46.6339);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<AuthClient> SignedInWithHomeAsync(this ApiFixture fixture, GeoPoint? home = null, string name = "Ana Leitora")
    {
        var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync(name: name);
        var point = home ?? Center;
        using var response = await client.PutAsync("/api/users/me/home-area", new { label = "Centro", latitude = point.Latitude, longitude = point.Longitude });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    /// <summary>Creates a listing for a manually entered book at <paramref name="at"/> (default: the owner's home).</summary>
    public static async Task<Guid> CreateListingAsync(
        this AuthClient client,
        string title = "Dom Casmurro",
        string[]? authors = null,
        string? isbn = null,
        GeoPoint? at = null,
        string condition = "Good",
        string category = "Fiction",
        bool goodForBeginners = false)
    {
        using var response = await client.PostAsync("/api/listings", new
        {
            manualBook = new { isbn, title, authors = authors ?? ["Machado de Assis"] },
            condition,
            category,
            goodForBeginners,
            description = "Bem conservado.",
            areaLabel = at is null ? null : "Bairro",
            latitude = at?.Latitude,
            longitude = at?.Longitude,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.JsonAsync()).RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<HttpResponseMessage> UploadImageAsync(this AuthClient client, Guid listingId, byte[] bytes, string fileName = "photo.jpg", string contentType = "image/jpeg")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/listings/{listingId}/images") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", client.AccessToken);
        return await client.Http.SendAsync(request, Ct);
    }

    public static async Task<JsonDocument> JsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
}
