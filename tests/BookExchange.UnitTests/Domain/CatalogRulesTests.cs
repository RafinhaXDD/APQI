using BookExchange.Domain.Books;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;

namespace BookExchange.UnitTests.Domain;

public sealed class IsbnTests
{
    [Theory]
    [InlineData("9788535914849", "9788535914849")]
    [InlineData("978-85-359-1484-9", "9788535914849")]
    [InlineData(" 978 85 359 1484 9 ", "9788535914849")]
    [InlineData("0-306-40615-2", "9780306406157")]
    [InlineData("080442957X", "9780804429573")]
    [InlineData("080442957x", "9780804429573")]
    public void Valid_isbns_normalize_to_isbn13(string input, string expected) =>
        Isbn.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("9788535914848")]
    [InlineData("0306406153")]
    [InlineData("12345")]
    [InlineData("97885359148491")]
    [InlineData("97885359148X9")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_isbns_are_rejected(string? input) => Isbn.Normalize(input).Should().BeNull();
}

public sealed class PublicLocationTests
{
    private static readonly GeoPoint Origin = new(-23.5503, -46.6339);

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(999, 1, true)]
    [InlineData(1000, 1, false)]
    [InlineData(1240, 1, false)]
    [InlineData(1250, 1.5, false)]
    [InlineData(3740, 3.5, false)]
    [InlineData(3760, 4, false)]
    public void Distances_round_to_half_kilometres_with_a_less_than_1_km_floor(double meters, double km, bool upperBound) =>
        PublicLocation.RoundDistance(meters).Should().Be((km, upperBound));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.25)]
    [InlineData(1, 0.999)]
    public void Jitter_moves_the_point_between_300_and_700_metres(double unitDistance, double unitBearing)
    {
        var jittered = PublicLocation.Jitter(Origin, unitDistance, unitBearing);

        Haversine(Origin, jittered).Should().BeInRange(299, 701);
    }

    [Fact]
    public void Offset_lands_at_the_requested_distance()
    {
        var moved = PublicLocation.Offset(Origin, 5000, 123);

        Haversine(Origin, moved).Should().BeApproximately(5000, 1);
    }

    private static double Haversine(GeoPoint a, GeoPoint b)
    {
        const double r = 6_371_008.8;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(b.Latitude - a.Latitude);
        var dLon = Rad(b.Longitude - a.Longitude);
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) + (Math.Cos(Rad(a.Latitude)) * Math.Cos(Rad(b.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * r * Math.Asin(Math.Sqrt(h));
    }
}

public sealed class ListingRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Here = new(-23.55, -46.63);

    private static Listing NewListing() =>
        Listing.Create(Guid.NewGuid(), Guid.NewGuid(), "  ok  ", BookCondition.Good, BookCategory.Fiction, false, " Centro ", Here, Here, Now);

    [Fact]
    public void A_new_listing_is_active_and_trimmed()
    {
        var listing = NewListing();

        listing.Status.Should().Be(ListingStatus.Active);
        listing.Description.Should().Be("ok");
        listing.AreaLabel.Should().Be("Centro");
    }

    [Fact]
    public void Archived_listings_cannot_be_edited_archived_again_or_given_photos()
    {
        var listing = NewListing();
        listing.Archive(Now);

        listing.Invoking(l => l.UpdateDetails("x", BookCondition.Fair, BookCategory.Fiction, false, Now)).Should().Throw<DomainException>();
        listing.Invoking(l => l.Archive(Now)).Should().Throw<DomainException>();
        listing.Invoking(l => l.AddImage(Guid.NewGuid(), "d", "t", 1, 1, Now)).Should().Throw<DomainException>();
    }

    [Fact]
    public void At_most_8_photos_with_increasing_positions()
    {
        var listing = NewListing();
        for (var i = 0; i < Listing.MaxImages; i++)
        {
            listing.AddImage(Guid.NewGuid(), $"d{i}", $"t{i}", 10, 10, Now);
        }

        listing.Images.Select(i => i.Position).Should().Equal(0, 1, 2, 3, 4, 5, 6, 7);
        listing.Invoking(l => l.AddImage(Guid.NewGuid(), "d", "t", 1, 1, Now)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Description_over_the_limit_and_blank_area_are_rejected()
    {
        var act1 = () => Listing.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', Listing.DescriptionMaxLength + 1), BookCondition.Good, BookCategory.Fiction, false, "A", Here, Here, Now);
        var act2 = () => Listing.Create(Guid.NewGuid(), Guid.NewGuid(), null, BookCondition.Good, BookCategory.Fiction, false, "  ", Here, Here, Now);

        act1.Should().Throw<DomainException>();
        act2.Should().Throw<DomainException>();
    }

    [Fact]
    public void Book_keeps_only_https_covers_and_drops_blank_or_duplicate_authors()
    {
        var book = Book.Create("9788535914849", " Título ", ["A", " ", "A", "B"], 1995, "http://insecure.example/cover.jpg", BookSource.Manual, Now);

        book.Title.Should().Be("Título");
        book.Authors.Should().Equal("A", "B");
        book.CoverUrl.Should().BeNull();
    }
}
