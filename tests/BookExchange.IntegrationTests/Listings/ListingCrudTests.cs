using System.Net;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Listings;

public sealed class ListingCrudTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_defaults_to_the_home_area_and_only_the_owner_sees_the_exact_point()
    {
        using var owner = await fixture.SignedInWithHomeAsync();

        using var created = await owner.PostAsync("/api/listings", new
        {
            manualBook = new { title = "Grande Sertão: Veredas", authors = new[] { "João Guimarães Rosa" } },
            category = "Fiction", condition = "LikeNew",
        });
        using var body = await created.JsonAsync();
        var id = body.RootElement.GetProperty("id").GetGuid();

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.ToString().Should().Be($"/api/listings/{id}");
        body.RootElement.GetProperty("status").GetString().Should().Be("Active");
        body.RootElement.GetProperty("condition").GetString().Should().Be("LikeNew");
        body.RootElement.GetProperty("areaLabel").GetString().Should().Be("Centro");
        body.RootElement.GetProperty("isMine").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("myLocation").GetProperty("latitude").GetDouble().Should().BeApproximately(CatalogHelpers.Center.Latitude, 1e-9);
    }

    [Fact]
    public async Task Public_point_is_shifted_300_to_700_metres_from_the_exact_point()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();

        var meters = await fixture.WithDbAsync(db => db.Database
            .SqlQuery<double>($"""SELECT ST_Distance("Location", "PublicPoint") AS "Value" FROM "Listings" WHERE "Id" = {id}""")
            .SingleAsync(Ct));

        meters.Should().BeInRange(299, 701);
    }

    [Fact]
    public async Task Without_home_area_or_coordinates_the_listing_needs_a_location()
    {
        using var owner = fixture.CreateAuthClient();
        await owner.SignUpAndLoginAsync();

        using var response = await owner.PostAsync("/api/listings", new
        {
            manualBook = new { title = "Memórias Póstumas", authors = new[] { "Machado de Assis" } },
            category = "Fiction", condition = "Good",
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("listing.location_required");
    }

    [Fact]
    public async Task Manual_entry_with_a_known_isbn_reuses_the_stored_book()
    {
        using var owner = await fixture.SignedInWithHomeAsync();

        var first = await owner.CreateListingAsync(title: "O Alienista", isbn: "9788525406859");
        var second = await owner.CreateListingAsync(title: "different title typed by someone", isbn: "978-85-254-0685-9");

        var bookIds = await fixture.WithDbAsync(db => db.Listings
            .Where(l => l.Id == first || l.Id == second).Select(l => l.BookId).Distinct().ToListAsync(Ct));
        bookIds.Should().ContainSingle();
    }

    [Fact]
    public async Task Category_is_required_and_can_be_changed_with_the_beginners_tag()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync(category: "Classics");

        using var missing = await owner.PostAsync("/api/listings", new { manualBook = new { title = "Sem categoria", authors = new[] { "X" } }, condition = "Good" });
        using var update = await owner.PutAsync($"/api/listings/{id}", new { condition = "Good", category = "PersonalDevelopment", goodForBeginners = true });
        using var body = await update.JsonAsync();

        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await missing.Content.ReadAsStringAsync(Ct)).Should().Contain("\"category\"");
        body.RootElement.GetProperty("category").GetString().Should().Be("PersonalDevelopment");
        body.RootElement.GetProperty("goodForBeginners").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Exactly_one_of_bookId_or_manualBook_is_required()
    {
        using var owner = await fixture.SignedInWithHomeAsync();

        using var neither = await owner.PostAsync("/api/listings", new { category = "Fiction", condition = "Good" });
        using var unknownBook = await owner.PostAsync("/api/listings", new { bookId = Guid.NewGuid(), category = "Fiction", condition = "Good" });

        neither.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknownBook.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await unknownBook.ProblemCodeAsync()).Should().Be("book.not_found");
    }

    [Fact]
    public async Task Only_the_owner_can_edit_or_archive_and_others_get_403_on_a_public_listing()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var other = await fixture.SignedInWithHomeAsync(name: "Bruno");
        var id = await owner.CreateListingAsync();

        using var edit = await other.PutAsync($"/api/listings/{id}", new { category = "Fiction", condition = "Worn", description = "hijacked" });
        using var archive = await other.PostAsync($"/api/listings/{id}/archive", new { });
        using var upload = await other.UploadImageAsync(id, TestImages.Jpeg());

        edit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        archive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fixture.WithDbAsync(db => db.Listings.SingleAsync(l => l.Id == id, Ct))).Condition.Should().Be(BookCondition.Good);
    }

    [Fact]
    public async Task Archived_listing_is_hidden_from_others_404_but_still_visible_to_the_owner()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var other = await fixture.SignedInWithHomeAsync(name: "Carla");
        using var anonymous = fixture.CreateAuthClient();
        var id = await owner.CreateListingAsync();

        (await owner.PostAsync($"/api/listings/{id}/archive", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var ownerView = await owner.GetAsync($"/api/listings/{id}");
        using var otherView = await other.GetAsync($"/api/listings/{id}");
        using var anonymousView = await anonymous.GetAsync($"/api/listings/{id}");
        using var otherEdit = await other.PutAsync($"/api/listings/{id}", new { category = "Fiction", condition = "Worn" });
        using var ownerEdit = await owner.PutAsync($"/api/listings/{id}", new { category = "Fiction", condition = "Worn" });

        ownerView.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ownerView.JsonAsync()).RootElement.GetProperty("status").GetString().Should().Be("Archived");
        otherView.StatusCode.Should().Be(HttpStatusCode.NotFound);
        anonymousView.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherEdit.StatusCode.Should().Be(HttpStatusCode.NotFound, "existence of a non-public listing isn't revealed (R-17)");
        ownerEdit.StatusCode.Should().Be(HttpStatusCode.Conflict, "an archived listing can't be changed");
    }

    [Fact]
    public async Task Update_changes_details_moving_re_jitters_and_renaming_keeps_the_public_point()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        var id = await owner.CreateListingAsync();
        var original = await PublicPointAsync(id);

        using var rename = await owner.PutAsync($"/api/listings/{id}", new { category = "Fiction", condition = "Fair", description = "Capa gasta", areaLabel = "Sé" });
        var afterRename = await PublicPointAsync(id);
        var moveTo = PublicLocation.Offset(CatalogHelpers.Center, 5000, 90);
        using var move = await owner.PutAsync($"/api/listings/{id}", new { category = "Fiction", condition = "Fair", latitude = moveTo.Latitude, longitude = moveTo.Longitude });
        var afterMove = await PublicPointAsync(id);

        rename.StatusCode.Should().Be(HttpStatusCode.OK);
        using var renamed = await rename.JsonAsync();
        renamed.RootElement.GetProperty("condition").GetString().Should().Be("Fair");
        renamed.RootElement.GetProperty("description").GetString().Should().Be("Capa gasta");
        renamed.RootElement.GetProperty("areaLabel").GetString().Should().Be("Sé");
        afterRename.Should().Be(original);
        move.StatusCode.Should().Be(HttpStatusCode.OK);
        afterMove.Should().NotBe(original);
    }

    [Fact]
    public async Task Server_controlled_fields_in_the_body_are_ignored()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var other = await fixture.SignedInWithHomeAsync(name: "Dani");
        var otherId = (await other.GetAsync("/api/auth/me").ContinueWith(t => t.Result.JsonAsync()).Unwrap()).RootElement.GetProperty("id").GetGuid();

        using var created = await owner.PostAsync("/api/listings", new
        {
            manualBook = new { title = "Quincas Borba", authors = new[] { "Machado de Assis" } },
            category = "Fiction", condition = "Good",
            status = "Exchanged",
            ownerId = otherId,
        });
        var id = (await created.JsonAsync()).RootElement.GetProperty("id").GetGuid();

        var listing = await fixture.WithDbAsync(db => db.Listings.SingleAsync(l => l.Id == id, Ct));
        listing.Status.Should().Be(ListingStatus.Active);
        listing.OwnerId.Should().NotBe(otherId);
    }

    [Fact]
    public async Task My_listings_include_every_status_and_nobody_elses()
    {
        using var owner = await fixture.SignedInWithHomeAsync();
        using var other = await fixture.SignedInWithHomeAsync(name: "Eva");
        var active = await owner.CreateListingAsync(title: "Iracema");
        var archived = await owner.CreateListingAsync(title: "Senhora");
        await other.CreateListingAsync(title: "Not mine");
        await owner.PostAsync($"/api/listings/{archived}/archive", new { });

        using var response = await owner.GetAsync("/api/listings/mine");
        using var body = await response.JsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = body.RootElement.EnumerateArray().Select(e => (e.GetProperty("id").GetGuid(), e.GetProperty("status").GetString())).ToList();
        items.Should().BeEquivalentTo([(archived, "Archived"), (active, "Active")]);
    }

    private Task<(double, double)> PublicPointAsync(Guid id) => fixture.WithDbAsync(async db =>
    {
        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == id, Ct);
        return (listing.PublicPoint.Latitude, listing.PublicPoint.Longitude);
    });
}
