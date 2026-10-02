using System.Net;
using BookExchange.IntegrationTests.Infrastructure;
using Testcontainers.Azurite;

namespace BookExchange.IntegrationTests.Listings;

/// <summary>
/// The API configured exactly like Docker Compose: photos in Azure Blob Storage (Azurite). Other photo
/// tests use local disk; this class keeps the Blob implementation honest.
/// </summary>
public sealed class AzuriteFixture(PostgisContainer postgis) : ApiFixture(postgis)
{
    // Keep in sync with docker-compose.yml.
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    protected override IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>
    {
        ["Storage:Provider"] = "Blob",
        ["Storage:BlobConnectionString"] = _azurite.GetConnectionString(),
        ["Storage:BlobContainer"] = "listing-images",
    };

    public override async ValueTask InitializeAsync()
    {
        await _azurite.StartAsync();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _azurite.DisposeAsync();
    }
}

public sealed class BlobStorageTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Photos_are_uploaded_served_and_deleted_through_blob_storage()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var anonymous = fixture.CreateAuthClient();
        var id = await owner.CreateListingAsync();

        using var upload = await owner.UploadImageAsync(id, TestImages.Jpeg(1600, 1200, withGps: true));
        using var uploaded = await upload.JsonAsync();
        var imageId = uploaded.RootElement.GetProperty("id").GetGuid();
        using var display = await anonymous.GetAsync(uploaded.RootElement.GetProperty("displayUrl").GetString()!);
        var bytes = await display.Content.ReadAsByteArrayAsync(Ct);
        using var delete = await owner.SendAsync(HttpMethod.Delete, $"/api/listings/{id}/images/{imageId}", body: null);
        using var afterDelete = await anonymous.GetAsync($"/api/listing-images/{imageId}/display");

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        display.StatusCode.Should().Be(HttpStatusCode.OK);
        display.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        TestImages.HasExif(bytes).Should().BeFalse();
        TestImages.Identify(bytes).Width.Should().Be(1280);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
