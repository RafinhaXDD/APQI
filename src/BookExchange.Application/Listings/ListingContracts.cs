using BookExchange.Application.Books;
using BookExchange.Domain.Listings;

namespace BookExchange.Application.Listings;

/// <summary>Either a book from the ISBN lookup (<see cref="BookId"/>) or a manual entry.</summary>
/// <param name="Latitude">Optional: defaults to the owner's home area.</param>
public sealed record CreateListingRequest(
    Guid? BookId,
    ManualBookRequest? ManualBook,
    BookCondition Condition,
    BookCategory? Category,
    bool GoodForBeginners,
    string? Description,
    string? AreaLabel,
    double? Latitude,
    double? Longitude);

public sealed record UpdateListingRequest(
    BookCondition Condition,
    BookCategory? Category,
    bool GoodForBeginners,
    string? Description,
    string? AreaLabel,
    double? Latitude,
    double? Longitude);

public enum ListingSort
{
    Distance,
    Newest,
    Title,
}

/// <param name="Lat">The searcher's own position (never stored). Defaults to the signed-in user's home area.</param>
public sealed record ListingSearchRequest(
    double? Lat,
    double? Lng,
    double? RadiusKm,
    string? Q,
    string? Isbn,
    BookCondition? Condition,
    BookCategory? Category,
    bool? Beginners,
    ListingSort? Sort,
    int? Page,
    int? PageSize);

/// <summary>Public search result. No coordinates: only a rounded distance and the area label (R-14).</summary>
public sealed record ListingSummary(
    Guid Id,
    string Title,
    IReadOnlyList<string> Authors,
    string? CoverUrl,
    string? ThumbnailUrl,
    BookCondition Condition,
    BookCategory Category,
    bool GoodForBeginners,
    string AreaLabel,
    double DistanceKm,
    bool DistanceIsUpperBound,
    string OwnerDisplayName,
    bool IsMine,
    DateTimeOffset CreatedAt);

public sealed record ListingImageDto(Guid Id, string DisplayUrl, string ThumbnailUrl, int Width, int Height);

public sealed record PublicDistance(double Km, bool IsUpperBound);

/// <summary>Exact location, returned only to the owner.</summary>
public sealed record OwnerLocation(string AreaLabel, double Latitude, double Longitude);

public sealed record ListingOwner(Guid Id, string DisplayName);

public sealed record ListingDetails(
    Guid Id,
    BookDto Book,
    string? Description,
    BookCondition Condition,
    BookCategory Category,
    bool GoodForBeginners,
    ListingStatus Status,
    string AreaLabel,
    PublicDistance? Distance,
    IReadOnlyList<ListingImageDto> Images,
    ListingOwner Owner,
    bool IsMine,
    OwnerLocation? MyLocation,
    DateTimeOffset CreatedAt);

public sealed record MyListingItem(
    Guid Id,
    string Title,
    IReadOnlyList<string> Authors,
    string? CoverUrl,
    string? ThumbnailUrl,
    BookCondition Condition,
    BookCategory Category,
    bool GoodForBeginners,
    ListingStatus Status,
    string AreaLabel,
    int ImageCount,
    DateTimeOffset CreatedAt);

public static class ListingSearchLimits
{
    public const double DefaultRadiusKm = 5;
    public const double MaxRadiusKm = 50;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;
}

public static class ImageUrls
{
    public static string Display(Guid imageId) => $"/api/listing-images/{imageId}/display";

    public static string Thumbnail(Guid imageId) => $"/api/listing-images/{imageId}/thumb";
}
