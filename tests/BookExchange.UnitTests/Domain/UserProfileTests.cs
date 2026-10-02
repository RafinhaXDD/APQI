using BookExchange.Domain.Shared;
using BookExchange.Domain.Users;

namespace BookExchange.UnitTests.Domain;

public sealed class UserProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_the_display_name_and_has_no_home_area()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "  Ana  ", "pt-BR", Now);

        profile.DisplayName.Should().Be("Ana");
        profile.HasHomeArea.Should().BeFalse();
        profile.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("   ")]
    public void Too_short_display_names_are_rejected(string name)
    {
        var act = () => UserProfile.Create(Guid.NewGuid(), name, "en", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Unsupported_language_is_rejected()
    {
        var act = () => UserProfile.Create(Guid.NewGuid(), "Ana", "fr", Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Blank_bio_is_stored_as_null()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Ana", "en", Now);

        profile.Update("Ana", "   ", "en", Now);

        profile.Bio.Should().BeNull();
    }

    [Fact]
    public void Set_home_area_stores_label_and_point()
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Ana", "en", Now);

        profile.SetHomeArea(" Centro ", new GeoPoint(-23.5, -46.6), Now.AddMinutes(1));

        profile.HomeAreaLabel.Should().Be("Centro");
        profile.HomePoint.Should().Be(new GeoPoint(-23.5, -46.6));
        profile.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Theory]
    [InlineData(90.0001, 0)]
    [InlineData(-90.0001, 0)]
    [InlineData(0, 180.0001)]
    [InlineData(double.NaN, 0)]
    public void Geo_point_outside_wgs84_ranges_is_rejected(double latitude, double longitude)
    {
        var act = () => new GeoPoint(latitude, longitude);

        act.Should().Throw<DomainException>();
    }
}
