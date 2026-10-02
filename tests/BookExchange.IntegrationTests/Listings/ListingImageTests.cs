using System.Net;
using System.Text;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Listings;

/// <summary>SPEC §6.10 and §12: magic bytes, size, count, EXIF stripped, GUID storage names.</summary>
public sealed class ListingImageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gps_tagged_jpeg_is_stored_without_any_exif_resized_and_under_a_generated_name()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        var original = TestImages.Jpeg(2400, 1600, withGps: true);
        TestImages.HasExif(original).Should().BeTrue("the fixture must really carry GPS data");

        using var response = await owner.UploadImageAsync(id, original, fileName: "my-secret-home-photo.jpg");
        using var body = await response.JsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var image = await fixture.WithDbAsync(db => db.Listings.SelectMany(l => l.Images).SingleAsync(i => i.ListingId == id, Ct));
        image.DisplayKey.Should().NotContain("secret").And.StartWith($"listings/{id}/");
        var stored = await File.ReadAllBytesAsync(Path.Combine(fixture.Factory.StorageRoot, image.DisplayKey), Ct);
        TestImages.HasExif(stored).Should().BeFalse();
        TestImages.Identify(stored).Should().Be((1280, 853, "Webp"));
        body.RootElement.GetProperty("width").GetInt32().Should().Be(1280);
    }

    [Fact]
    public async Task Photos_are_served_anonymously_with_long_caching()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var anonymous = fixture.CreateAuthClient();
        var id = await owner.CreateListingAsync();
        using var upload = await owner.UploadImageAsync(id, TestImages.Png(), "cover.png", "image/png");
        using var uploaded = await upload.JsonAsync();
        var thumbUrl = uploaded.RootElement.GetProperty("thumbnailUrl").GetString()!;

        using var thumb = await anonymous.GetAsync(thumbUrl);
        var bytes = await thumb.Content.ReadAsByteArrayAsync(Ct);

        thumb.StatusCode.Should().Be(HttpStatusCode.OK);
        thumb.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        thumb.Headers.CacheControl!.ToString().Should().Contain("immutable");
        TestImages.Identify(bytes).Width.Should().BeLessThanOrEqualTo(320);
    }

    [Fact]
    public async Task Png_and_webp_are_accepted()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();

        using var png = await owner.UploadImageAsync(id, TestImages.Png(), "a.png", "image/png");
        using var webp = await owner.UploadImageAsync(id, TestImages.Webp(), "b.webp", "image/webp");

        png.StatusCode.Should().Be(HttpStatusCode.Created);
        webp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_non_image_is_rejected_by_its_content_whatever_its_name_or_content_type()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        var fake = Encoding.UTF8.GetBytes("<script>alert('not an image')</script>");
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

        using var text = await owner.UploadImageAsync(id, fake, "photo.jpg", "image/jpeg");
        using var gifUpload = await owner.UploadImageAsync(id, gif, "photo.jpg", "image/jpeg");

        text.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await text.ProblemCodeAsync()).Should().Be("image.invalid");
        gifUpload.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "GIF is a real image but not an allowed format");
        (await fixture.WithDbAsync(db => db.Listings.SelectMany(l => l.Images).CountAsync(i => i.ListingId == id, Ct))).Should().Be(0);
    }

    [Fact]
    public async Task Files_over_5_mb_are_rejected()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        var big = new byte[(5 * 1024 * 1024) + 1];
        TestImages.Jpeg().CopyTo(big, 0);

        using var response = await owner.UploadImageAsync(id, big);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("image.too_large");
    }

    [Fact]
    public async Task A_listing_holds_at_most_8_photos()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        var photo = TestImages.Jpeg(200, 150);

        for (var i = 0; i < 8; i++)
        {
            (await owner.UploadImageAsync(id, photo)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var ninth = await owner.UploadImageAsync(id, photo);

        ninth.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await fixture.WithDbAsync(db => db.Listings.SelectMany(l => l.Images).CountAsync(i => i.ListingId == id, Ct))).Should().Be(8);
    }

    [Fact]
    public async Task Removing_a_photo_deletes_the_row_and_both_files()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        using var upload = await owner.UploadImageAsync(id, TestImages.Jpeg());
        var imageId = (await upload.JsonAsync()).RootElement.GetProperty("id").GetGuid();
        var image = await fixture.WithDbAsync(db => db.Listings.SelectMany(l => l.Images).SingleAsync(i => i.Id == imageId, Ct));

        using var delete = await owner.SendAsync(HttpMethod.Delete, $"/api/listings/{id}/images/{imageId}", body: null);

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(Path.Combine(fixture.Factory.StorageRoot, image.DisplayKey)).Should().BeFalse();
        File.Exists(Path.Combine(fixture.Factory.StorageRoot, image.ThumbnailKey)).Should().BeFalse();
        (await owner.GetAsync($"/api/listing-images/{imageId}/display")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
